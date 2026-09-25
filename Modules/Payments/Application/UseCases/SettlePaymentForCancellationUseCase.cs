using OrderCore.Api.Modules.AuditLogs.Application.Constants;
using OrderCore.Api.Modules.AuditLogs.Application.Services;
using OrderCore.Api.Modules.Payments.Application.Contracts;
using OrderCore.Api.Modules.Payments.Application.DTOs;
using OrderCore.Api.Modules.Payments.Domain.Entities;
using OrderCore.Api.Modules.Payments.Domain.Enums;
using OrderCore.Api.Modules.Payments.Domain.Repositories;
using OrderCore.Api.Shared.Application.Exceptions;

namespace OrderCore.Api.Modules.Payments.Application.UseCases;

/// <summary>
/// Leaves no money held for an order that is being cancelled (backoffice
/// decision 2):
/// <list type="bullet">
/// <item>an authorized payment is voided, so the buyer is never charged;</item>
/// <item>a captured one is refunded for whatever is still held, through
/// the regular refund path (outbox <c>PaymentRefunded</c> included);</item>
/// <item>no payment, or a failed/voided/refunded one, needs nothing;</item>
/// <item>a payment still waiting on the provider can't be settled yet
/// (<c>409 payment_in_progress</c>): there is nothing to void until it
/// answers.</item>
/// </list>
/// Idempotent, since a settled payment ends in a state that needs nothing,
/// so Orders can repeat a cancellation that failed after this step. A
/// provider refusal is a <c>409</c> and leaves the payment as it was.
/// </summary>
public sealed class SettlePaymentForCancellationUseCase
{
    private readonly IPaymentRepository _payments;
    private readonly IPaymentProvider _provider;
    private readonly RequestRefundUseCase _requestRefund;
    private readonly IAuditLogService _auditLog;
    private readonly TimeProvider _timeProvider;

    public SettlePaymentForCancellationUseCase(
        IPaymentRepository payments,
        IPaymentProvider provider,
        RequestRefundUseCase requestRefund,
        IAuditLogService auditLog,
        TimeProvider timeProvider)
    {
        _payments = payments;
        _provider = provider;
        _requestRefund = requestRefund;
        _auditLog = auditLog;
        _timeProvider = timeProvider;
    }

    public async Task<PaymentSettlementOutcome> ExecuteAsync(Guid orderId, string reason, CancellationToken cancellationToken)
    {
        var payment = await _payments.GetByOrderIdAsync(orderId, cancellationToken);

        switch (payment?.Status)
        {
            case null or PaymentStatus.Failed or PaymentStatus.Voided or PaymentStatus.Refunded:
                return PaymentSettlementOutcome.NothingToSettle;

            case PaymentStatus.Pending or PaymentStatus.Processing:
                throw new ConflictException(
                    "payment_in_progress",
                    $"The payment for order '{orderId}' is still being processed by the provider; try again shortly.");

            case PaymentStatus.Authorized:
                await VoidAsync(payment, reason, cancellationToken);
                return PaymentSettlementOutcome.Voided;

            case PaymentStatus.Captured:
                await RefundRemainingAsync(payment, reason, cancellationToken);
                return PaymentSettlementOutcome.Refunded;

            default:
                throw new InvalidOperationException($"Unhandled payment status '{payment.Status}'.");
        }
    }

    private async Task VoidAsync(Payment payment, string reason, CancellationToken cancellationToken)
    {
        var result = await _provider.VoidAsync(payment, cancellationToken);

        if (!result.Succeeded)
        {
            throw new ConflictException("payment_void_failed", $"Void failed for payment '{payment.Id}': {result.FailureReason}.");
        }

        payment.Void(_timeProvider.GetUtcNow());
        await _payments.SaveChangesAsync(cancellationToken);

        await _auditLog.RecordAsync(
            AuditLogActionNames.PaymentVoided,
            "Payment",
            payment.Id,
            new Dictionary<string, string?> { ["reason"] = reason },
            userId: null,
            cancellationToken);
    }

    private async Task RefundRemainingAsync(Payment payment, string reason, CancellationToken cancellationToken)
    {
        var alreadyRefunded = payment.Refunds.Where(r => r.Status != RefundStatus.Failed).Sum(r => r.Amount);
        var refund = await _requestRefund.ExecuteAsync(
            new RequestRefundCommand(payment.Id, payment.Amount - alreadyRefunded, reason), cancellationToken);

        if (refund.Status == RefundStatus.Failed)
        {
            throw new ConflictException("payment_refund_failed", $"Refund failed for payment '{payment.Id}'.");
        }
    }
}
