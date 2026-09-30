using OrderCore.Api.Modules.AuditLogs.Application.Constants;
using OrderCore.Api.Modules.AuditLogs.Application.Services;
using OrderCore.Api.Modules.Payments.Application.Contracts;
using OrderCore.Api.Modules.Payments.Application.DTOs;
using OrderCore.Api.Modules.Payments.Application.Telemetry;
using OrderCore.Api.Modules.Payments.Domain.Entities;
using OrderCore.Api.Modules.Payments.Domain.Enums;
using OrderCore.Api.Modules.Payments.Domain.Repositories;
using OrderCore.Api.Shared.Application.Exceptions;
using OrderCore.Api.Shared.Application.Observability;

namespace OrderCore.Api.Modules.Payments.Application.UseCases;

/// <summary>
/// Brings a payment in line with the provider (Stripe spec, reconciliation)
/// when a webhook may have been lost:
/// <list type="bullet">
/// <item>a payment the provider knows is asked where it stands, and the
/// answer goes through <see cref="ApplyPaymentProviderUpdateUseCase"/> —
/// the same transitions a webhook takes, so an update already applied (or
/// one the payment has moved past) changes nothing;</item>
/// <item>a payment the provider never answered for (no provider reference)
/// is authorized again through <see cref="AuthorizePaymentUseCase"/>: its
/// idempotency key makes the provider hand back what it created the first
/// time, if anything, instead of creating a second one.</item>
/// </list>
/// Refunds and disputes are not reconciled — only the payment's own status.
/// Measured (in sync or corrected) and audited when something changed.
/// </summary>
public sealed class ReconcilePaymentUseCase
{
    private readonly IPaymentRepository _payments;
    private readonly IPaymentProvider _provider;
    private readonly ApplyPaymentProviderUpdateUseCase _applyUpdate;
    private readonly AuthorizePaymentUseCase _authorize;
    private readonly IAuditLogService _auditLog;
    private readonly PaymentsMetrics _metrics;
    private readonly TimeProvider _timeProvider;

    public ReconcilePaymentUseCase(
        IPaymentRepository payments,
        IPaymentProvider provider,
        ApplyPaymentProviderUpdateUseCase applyUpdate,
        AuthorizePaymentUseCase authorize,
        IAuditLogService auditLog,
        PaymentsMetrics metrics,
        TimeProvider timeProvider)
    {
        _payments = payments;
        _provider = provider;
        _applyUpdate = applyUpdate;
        _authorize = authorize;
        _auditLog = auditLog;
        _metrics = metrics;
        _timeProvider = timeProvider;
    }

    public async Task<PaymentReconciliation> ExecuteAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        var payment = await _payments.GetByIdAsync(paymentId, cancellationToken)
            ?? throw new NotFoundException("payment_not_found", $"Payment '{paymentId}' was not found.");
        Observed.Payment(payment.Id);
        Observed.Order(payment.OrderId);

        var statusBefore = payment.Status;
        var declineBefore = payment.LastDeclineReason;
        string? providerStatus = null;

        if (payment.ProviderReference is not null)
        {
            var state = await _provider.GetStateAsync(payment, cancellationToken);
            providerStatus = state.Status.ToString();
            foreach (var update in UpdatesFor(payment, state))
            {
                await _applyUpdate.ExecuteAsync(update, cancellationToken);
            }
        }
        else if (payment.Status is PaymentStatus.Pending or PaymentStatus.Processing)
        {
            await _authorize.ExecuteAsync(payment.Id, cancellationToken);
        }

        // Read back: the updates were applied to the payment as saved.
        var after = await _payments.GetByIdAsync(paymentId, cancellationToken) ?? payment;
        var changed = after.Status != statusBefore || after.LastDeclineReason != declineBefore;
        _metrics.Reconciled(changed ? "corrected" : "in_sync");

        if (changed)
        {
            await _auditLog.RecordAsync(
                AuditLogActionNames.PaymentReconciled,
                "Payment",
                payment.Id,
                new Dictionary<string, string?>
                {
                    ["orderId"] = payment.OrderId.ToString(),
                    ["from"] = statusBefore.ToString(),
                    ["to"] = after.Status.ToString(),
                    ["providerStatus"] = providerStatus,
                },
                userId: null,
                cancellationToken);
        }

        return new PaymentReconciliation(payment.Id, statusBefore.ToString(), after.Status.ToString(), providerStatus, changed);
    }

    /// <summary>What the provider's state means as updates, in the order a webhook would have brought them.</summary>
    private IEnumerable<PaymentProviderUpdate> UpdatesFor(Payment payment, PaymentProviderState state)
    {
        var reference = payment.ProviderReference!;
        var now = _timeProvider.GetUtcNow();

        switch (state.Status)
        {
            case PaymentProviderStatus.WaitingForBuyer when state.DeclineReason is { } reason && reason != payment.LastDeclineReason:
                yield return new PaymentProviderUpdate(PaymentProviderUpdateKind.Declined, reference, now, Reason: reason);
                break;

            case PaymentProviderStatus.Authorized:
                yield return new PaymentProviderUpdate(
                    PaymentProviderUpdateKind.Authorized, reference, now, AuthorizationExpiresAt: state.AuthorizationExpiresAt);
                break;

            // Both steps: a missed "authorized" first (ignored if it wasn't missed), then the capture.
            case PaymentProviderStatus.Captured:
                yield return new PaymentProviderUpdate(PaymentProviderUpdateKind.Authorized, reference, now);
                yield return new PaymentProviderUpdate(PaymentProviderUpdateKind.Captured, reference, now);
                break;

            case PaymentProviderStatus.Canceled:
                yield return new PaymentProviderUpdate(
                    PaymentProviderUpdateKind.Canceled, reference, now, AuthorizationExpired: state.AuthorizationExpired);
                break;
        }
    }
}
