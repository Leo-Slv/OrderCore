using OrderCore.Api.Modules.AuditLogs.Application.Constants;
using OrderCore.Api.Modules.AuditLogs.Application.Services;
using OrderCore.Api.Modules.Payments.Application.Contracts;
using OrderCore.Api.Modules.Payments.Application.DTOs;
using OrderCore.Api.Modules.Payments.Application.Telemetry;
using OrderCore.Api.Modules.Payments.Contracts.IntegrationEvents;
using OrderCore.Api.Modules.Payments.Domain.Entities;
using OrderCore.Api.Modules.Payments.Domain.Enums;
using OrderCore.Api.Shared.Application.Observability;

namespace OrderCore.Api.Modules.Payments.Application.UseCases;

/// <summary>
/// Applies what the provider says happened to a payment
/// (Docs/specs/payments/stripe-provider.md) — the one code path for "the
/// provider says X", used by the Stripe webhook now and by reconciliation
/// later:
/// <list type="bullet">
/// <item><b>authorized</b> — a payment waiting for the buyer is
/// <c>Authorized</c>; <c>PaymentAuthorized</c> lets Orders confirm the order;</item>
/// <item><b>declined</b> — the decline is recorded, the payment keeps
/// waiting for the buyer to try another card (decision 9);</item>
/// <item><b>captured</b> — an authorized payment captured at the provider
/// (normally OrderCore captured it itself and this changes nothing);</item>
/// <item><b>canceled</b> — an authorized payment is voided, and when the
/// authorization expired <c>PaymentAuthorizationExpired</c> goes out too
/// (decision 6); a payment still waiting for the buyer fails, so Orders
/// ends the order and releases its stock;</item>
/// <item><b>refund succeeded/failed</b> — settles a refund still pending;</item>
/// <item><b>dispute opened</b> — recorded on the payment and audited.</item>
/// </list>
/// Tolerates updates that arrive late, twice or out of order: one the
/// payment has moved past is <see cref="PaymentProviderUpdateOutcome.Ignored"/>
/// (a late "authorized" for a voided payment changes nothing), never an error.
/// </summary>
public sealed class ApplyPaymentProviderUpdateUseCase
{
    private readonly IPaymentRepository _payments;
    private readonly IPaymentsOutbox _outbox;
    private readonly IAuditLogService _auditLog;
    private readonly PaymentsMetrics _metrics;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ApplyPaymentProviderUpdateUseCase> _logger;

    public ApplyPaymentProviderUpdateUseCase(
        IPaymentRepository payments,
        IPaymentsOutbox outbox,
        IAuditLogService auditLog,
        PaymentsMetrics metrics,
        TimeProvider timeProvider,
        ILogger<ApplyPaymentProviderUpdateUseCase> logger)
    {
        _payments = payments;
        _outbox = outbox;
        _auditLog = auditLog;
        _metrics = metrics;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<PaymentProviderUpdateOutcome> ExecuteAsync(PaymentProviderUpdate update, CancellationToken cancellationToken)
    {
        var kind = KindTag(update.Kind);
        var payment = await _payments.GetByProviderReferenceAsync(update.ProviderReference, cancellationToken);
        if (payment is null)
        {
            _logger.LogWarning("No payment has provider reference {ProviderReference}; the {UpdateKind} update is ignored.", update.ProviderReference, kind);
            _metrics.ProviderUpdate(kind, "unknown_payment");
            return PaymentProviderUpdateOutcome.UnknownPayment;
        }

        Observed.Payment(payment.Id);
        Observed.Order(payment.OrderId);

        var audit = Apply(payment, update);
        if (audit is null)
        {
            _logger.LogInformation(
                "The {UpdateKind} update changes nothing for payment {PaymentId} in status {PaymentStatus}.", kind, payment.Id, payment.Status);
            _metrics.ProviderUpdate(kind, "ignored");
            return PaymentProviderUpdateOutcome.Ignored;
        }

        await _payments.SaveChangesAsync(cancellationToken);
        _metrics.ProviderUpdate(kind, "applied");
        RecordMetrics(payment, update);

        await _auditLog.RecordAsync(audit.Value.Action, "Payment", payment.Id, audit.Value.Metadata, userId: null, cancellationToken);
        return PaymentProviderUpdateOutcome.Applied;
    }

    /// <summary>Changes the payment and enqueues what it publishes; null when the update changes nothing.</summary>
    private (string Action, Dictionary<string, string?> Metadata)? Apply(Payment payment, PaymentProviderUpdate update)
    {
        var now = _timeProvider.GetUtcNow();
        var metadata = new Dictionary<string, string?> { ["orderId"] = payment.OrderId.ToString(), ["source"] = "provider" };

        switch (update.Kind)
        {
            case PaymentProviderUpdateKind.Authorized when payment.IsAwaitingBuyer:
                payment.Authorize(update.ProviderReference, now);
                _outbox.Enqueue(new PaymentAuthorized
                {
                    EventId = Guid.NewGuid(),
                    Version = 1,
                    OccurredAt = now,
                    OrderId = payment.OrderId,
                    PaymentId = payment.Id,
                    Amount = payment.Amount,
                    Currency = payment.Currency,
                });
                metadata["amount"] = payment.Amount.ToString();
                return (AuditLogActionNames.PaymentAuthorized, metadata);

            case PaymentProviderUpdateKind.Declined when payment.IsAwaitingBuyer:
                payment.RecordDecline(update.Reason ?? "declined", update.OccurredAt);
                metadata["reason"] = payment.LastDeclineReason;
                return (AuditLogActionNames.PaymentDeclined, metadata);

            case PaymentProviderUpdateKind.Captured when payment.Status == PaymentStatus.Authorized:
                payment.Capture(now);
                _outbox.Enqueue(new PaymentCaptured
                {
                    EventId = Guid.NewGuid(),
                    Version = 1,
                    OccurredAt = now,
                    OrderId = payment.OrderId,
                    PaymentId = payment.Id,
                    Amount = payment.Amount,
                    Currency = payment.Currency,
                });
                return (AuditLogActionNames.PaymentCaptured, metadata);

            case PaymentProviderUpdateKind.Canceled when payment.Status == PaymentStatus.Authorized:
                payment.Void(now);
                _outbox.Enqueue(new PaymentVoided { EventId = Guid.NewGuid(), Version = 1, OccurredAt = now, OrderId = payment.OrderId, PaymentId = payment.Id });
                if (!update.AuthorizationExpired)
                {
                    return (AuditLogActionNames.PaymentVoided, metadata);
                }

                _outbox.Enqueue(new PaymentAuthorizationExpired
                {
                    EventId = Guid.NewGuid(),
                    Version = 1,
                    OccurredAt = now,
                    OrderId = payment.OrderId,
                    PaymentId = payment.Id,
                });
                return (AuditLogActionNames.PaymentAuthorizationExpired, metadata);

            case PaymentProviderUpdateKind.Canceled when payment.IsAwaitingBuyer:
                payment.Fail(payment.LastDeclineReason ?? "payment_canceled");
                _outbox.Enqueue(new PaymentFailed
                {
                    EventId = Guid.NewGuid(),
                    Version = 1,
                    OccurredAt = now,
                    OrderId = payment.OrderId,
                    PaymentId = payment.Id,
                    Reason = payment.FailureReason!,
                });
                metadata["reason"] = payment.FailureReason;
                return (AuditLogActionNames.PaymentFailed, metadata);

            case PaymentProviderUpdateKind.RefundSucceeded or PaymentProviderUpdateKind.RefundFailed
                when update.RefundId is { } refundId:
                var succeeded = update.Kind == PaymentProviderUpdateKind.RefundSucceeded;
                if (!payment.SettleRefund(refundId, succeeded, update.Reason, now))
                {
                    WarnIfCompletedRefundFailed(payment, refundId, succeeded);
                    return null;
                }

                if (succeeded)
                {
                    _outbox.Enqueue(new PaymentRefunded { EventId = Guid.NewGuid(), Version = 1, OccurredAt = now, OrderId = payment.OrderId, PaymentId = payment.Id });
                }

                metadata["refundId"] = refundId.ToString();
                metadata["outcome"] = succeeded ? "completed" : "failed";
                return (AuditLogActionNames.PaymentRefundSettled, metadata);

            case PaymentProviderUpdateKind.DisputeOpened when payment.MarkDisputed(update.OccurredAt):
                metadata["reason"] = update.Reason;
                return (AuditLogActionNames.PaymentDisputed, metadata);

            default:
                return null;
        }
    }

    /// <summary>A refund OrderCore already recorded as completed that the provider now reports failed needs a person.</summary>
    private void WarnIfCompletedRefundFailed(Payment payment, Guid refundId, bool succeeded)
    {
        if (!succeeded && payment.Refunds.Any(r => r.Id == refundId && r.Status == RefundStatus.Completed))
        {
            _logger.LogWarning("Refund {RefundId} of payment {PaymentId} was recorded as completed but the provider reports it failed.", refundId, payment.Id);
        }
    }

    private void RecordMetrics(Payment payment, PaymentProviderUpdate update)
    {
        var method = payment.Method.ToString();
        switch (update.Kind)
        {
            case PaymentProviderUpdateKind.Authorized:
                _metrics.Authorized(method);
                break;
            case PaymentProviderUpdateKind.Declined:
                _metrics.Declined(method, payment.LastDeclineReason);
                break;
            case PaymentProviderUpdateKind.Captured:
                _metrics.Captured();
                break;
            case PaymentProviderUpdateKind.Canceled when payment.Status == PaymentStatus.Voided:
                _metrics.Voided();
                break;
            case PaymentProviderUpdateKind.Canceled:
                _metrics.Declined(method, payment.FailureReason);
                break;
            case PaymentProviderUpdateKind.RefundSucceeded:
                _metrics.Refunded("completed");
                break;
            case PaymentProviderUpdateKind.RefundFailed:
                _metrics.Refunded("failed");
                break;
        }
    }

    /// <summary>The metric tag of an update kind.</summary>
    public static string KindTag(PaymentProviderUpdateKind kind) => kind switch
    {
        PaymentProviderUpdateKind.Authorized => "authorized",
        PaymentProviderUpdateKind.Declined => "declined",
        PaymentProviderUpdateKind.Captured => "captured",
        PaymentProviderUpdateKind.Canceled => "canceled",
        PaymentProviderUpdateKind.RefundSucceeded => "refund_succeeded",
        PaymentProviderUpdateKind.RefundFailed => "refund_failed",
        PaymentProviderUpdateKind.DisputeOpened => "dispute_opened",
        _ => "unknown",
    };
}
