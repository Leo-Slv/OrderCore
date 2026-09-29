using OrderCore.Api.Modules.AuditLogs.Application.Constants;
using OrderCore.Api.Modules.AuditLogs.Application.Services;
using OrderCore.Api.Modules.Payments.Application.Contracts;
using OrderCore.Api.Modules.Payments.Application.Telemetry;
using OrderCore.Api.Modules.Payments.Contracts.IntegrationEvents;
using OrderCore.Api.Modules.Payments.Application.DTOs;
using OrderCore.Api.Modules.Payments.Domain.Entities;
using OrderCore.Api.Modules.Payments.Domain.Repositories;
using OrderCore.Api.Shared.Application.Observability;
using OrderCore.Api.Shared.Domain.Exceptions;

namespace OrderCore.Api.Modules.Payments.Application.UseCases;

/// <summary>
/// Creates a <see cref="Payment"/> and asks the provider to authorize it in
/// the same call (resolved decision — see
/// Docs/specs/payments/payment-processing.md). The fake provider answers at
/// once: authorized (<c>PaymentAuthorized</c>) or declined
/// (<c>PaymentFailed</c>). Stripe answers "waiting for the buyer": the
/// payment stays <c>Processing</c> with its PaymentIntent, nothing is
/// published, and the result carries the <see cref="PaymentNextAction"/>
/// the storefront confirms the card with (Docs/specs/payments/stripe-provider.md).
/// <see cref="AuthorizePaymentUseCase"/> is the separate, explicit retry
/// path for a payment that got stuck before an outcome was recorded — this
/// use case never calls it.
/// </summary>
public sealed class CreatePaymentUseCase
{
    private readonly IPaymentRepository _payments;
    private readonly IPaymentProvider _provider;
    private readonly IPaymentsOutbox _outbox;
    private readonly IAuditLogService _auditLog;
    private readonly PaymentsMetrics _metrics;
    private readonly TimeProvider _timeProvider;

    public CreatePaymentUseCase(
        IPaymentRepository payments, IPaymentProvider provider, IPaymentsOutbox outbox, IAuditLogService auditLog, PaymentsMetrics metrics, TimeProvider timeProvider)
    {
        _payments = payments;
        _provider = provider;
        _outbox = outbox;
        _auditLog = auditLog;
        _metrics = metrics;
        _timeProvider = timeProvider;
    }

    public async Task<CreatePaymentResult> ExecuteAsync(CreatePaymentCommand command, CancellationToken cancellationToken)
    {
        // Checked before anything is created: a method the configured provider
        // doesn't take (Pix under Stripe) is refused, not attempted.
        if (!_provider.Info.SupportedMethods.Contains(command.Method))
        {
            throw new DomainRuleViolationException(
                "payment_method_unavailable", $"{command.Method} payments are not available with {_provider.Info.Name}.");
        }

        var now = _timeProvider.GetUtcNow();
        var payment = Payment.Create(
            command.OrderId, command.Amount, command.Currency, command.Method, command.IdempotencyKey, _provider.Info.Name, customerPaymentMethodId: null, now);

        await _payments.AddAsync(payment, cancellationToken);

        payment.MarkProcessing();
        var result = await _provider.AuthorizeAsync(payment, cancellationToken);

        if (result.RequiresBuyer)
        {
            // Nothing to publish yet: the outcome arrives once the buyer has
            // confirmed the card (Stripe's webhook).
            payment.AwaitBuyer(result.ProviderReference!, _timeProvider.GetUtcNow());
            await _payments.SaveChangesAsync(cancellationToken);
            Observed.Payment(payment.Id);
            Observed.Order(payment.OrderId);
            return new CreatePaymentResult(payment.Id, payment.Status.ToString(), PaymentNextAction.ConfirmCardWith(result.ClientSecret!));
        }

        if (result.Succeeded)
        {
            payment.Authorize(result.ProviderReference!, _timeProvider.GetUtcNow());
            _outbox.Enqueue(new PaymentAuthorized
            {
                EventId = Guid.NewGuid(),
                Version = 1,
                OccurredAt = _timeProvider.GetUtcNow(),
                OrderId = payment.OrderId,
                PaymentId = payment.Id,
                Amount = payment.Amount,
                Currency = payment.Currency,
            });
        }
        else
        {
            payment.Fail(result.FailureReason ?? "unknown_failure");
            _outbox.Enqueue(new PaymentFailed
            {
                EventId = Guid.NewGuid(),
                Version = 1,
                OccurredAt = _timeProvider.GetUtcNow(),
                OrderId = payment.OrderId,
                PaymentId = payment.Id,
                Reason = payment.FailureReason!,
            });
        }

        await _payments.SaveChangesAsync(cancellationToken);
        Observed.Payment(payment.Id);
        Observed.Order(payment.OrderId);
        if (result.Succeeded)
        {
            _metrics.Authorized(payment.Method.ToString());
        }
        else
        {
            _metrics.Declined(payment.Method.ToString(), payment.FailureReason);
        }

        if (result.Succeeded)
        {
            await _auditLog.RecordAsync(
                AuditLogActionNames.PaymentAuthorized,
                "Payment",
                payment.Id,
                new Dictionary<string, string?> { ["orderId"] = payment.OrderId.ToString(), ["amount"] = payment.Amount.ToString() },
                userId: null,
                cancellationToken);
        }
        else
        {
            await _auditLog.RecordAsync(
                AuditLogActionNames.PaymentFailed,
                "Payment",
                payment.Id,
                new Dictionary<string, string?> { ["orderId"] = payment.OrderId.ToString(), ["reason"] = payment.FailureReason },
                userId: null,
                cancellationToken);
        }

        return new CreatePaymentResult(payment.Id, payment.Status.ToString());
    }
}
