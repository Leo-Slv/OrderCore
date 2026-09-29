using OrderCore.Api.Modules.AuditLogs.Application.Constants;
using OrderCore.Api.Modules.AuditLogs.Application.Services;
using OrderCore.Api.Modules.Payments.Application.Contracts;
using OrderCore.Api.Modules.Payments.Application.Telemetry;
using OrderCore.Api.Modules.Payments.Contracts.IntegrationEvents;
using OrderCore.Api.Modules.Payments.Domain.Repositories;
using OrderCore.Api.Shared.Application.Observability;

namespace OrderCore.Api.Modules.Payments.Application.UseCases;

/// <summary>
/// The payment window (Stripe spec, decision 8): a payment still waiting for
/// the buyer some time after it was created is given up on — cancelled at the
/// provider, so the card can no longer be confirmed, and failed with the last
/// decline reason or <see cref="WindowExpiredReason"/>. <c>PaymentFailed</c>
/// then ends the order as <c>PaymentFailed</c> and releases its stock through
/// the path Orders already has.
/// <para>
/// Only payments waiting for the buyer: one stuck before the provider
/// answered (no provider reference) is left to reconciliation, which asks the
/// provider first. A payment the provider won't cancel is left as it is and
/// logged — its own webhook, or reconciliation, settles it.
/// </para>
/// </summary>
public sealed class ExpirePaymentWindowUseCase
{
    public const string WindowExpiredReason = "payment_window_expired";

    private readonly IPaymentRepository _payments;
    private readonly IPaymentProvider _provider;
    private readonly IPaymentsOutbox _outbox;
    private readonly IAuditLogService _auditLog;
    private readonly PaymentsMetrics _metrics;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ExpirePaymentWindowUseCase> _logger;

    public ExpirePaymentWindowUseCase(
        IPaymentRepository payments,
        IPaymentProvider provider,
        IPaymentsOutbox outbox,
        IAuditLogService auditLog,
        PaymentsMetrics metrics,
        TimeProvider timeProvider,
        ILogger<ExpirePaymentWindowUseCase> logger)
    {
        _payments = payments;
        _provider = provider;
        _outbox = outbox;
        _auditLog = auditLog;
        _metrics = metrics;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>The payments whose window is over, oldest first — each then expired on its own.</summary>
    public Task<IReadOnlyList<Guid>> FindExpiredAsync(TimeSpan window, int limit, CancellationToken cancellationToken) =>
        _payments.ListAwaitingBuyerCreatedBeforeAsync(_timeProvider.GetUtcNow() - window, limit, cancellationToken);

    /// <returns>Whether the payment was given up on; false when it has moved on meanwhile or the provider refused.</returns>
    public async Task<bool> ExpireAsync(Guid paymentId, TimeSpan window, CancellationToken cancellationToken)
    {
        var payment = await _payments.GetByIdAsync(paymentId, cancellationToken);
        if (payment is not { IsAwaitingBuyer: true } || payment.CreatedAt >= _timeProvider.GetUtcNow() - window)
        {
            return false;
        }

        Observed.Payment(payment.Id);
        Observed.Order(payment.OrderId);

        var cancelled = await _provider.VoidAsync(payment, cancellationToken);
        if (!cancelled.Succeeded)
        {
            _logger.LogWarning(
                "The provider would not cancel payment {PaymentId} past its window ({Reason}); leaving it to its webhook or reconciliation.",
                payment.Id, cancelled.FailureReason);
            return false;
        }

        payment.Fail(payment.LastDeclineReason ?? WindowExpiredReason);
        _outbox.Enqueue(new PaymentFailed
        {
            EventId = Guid.NewGuid(),
            Version = 1,
            OccurredAt = _timeProvider.GetUtcNow(),
            OrderId = payment.OrderId,
            PaymentId = payment.Id,
            Reason = payment.FailureReason!,
        });
        await _payments.SaveChangesAsync(cancellationToken);
        _metrics.Declined(payment.Method.ToString(), payment.FailureReason);

        await _auditLog.RecordAsync(
            AuditLogActionNames.PaymentFailed,
            "Payment",
            payment.Id,
            new Dictionary<string, string?>
            {
                ["orderId"] = payment.OrderId.ToString(),
                ["reason"] = payment.FailureReason,
                ["source"] = "payment_window",
            },
            userId: null,
            cancellationToken);
        return true;
    }
}
