using OrderCore.Api.Modules.AuditLogs.Application.Constants;
using OrderCore.Api.Modules.AuditLogs.Application.Services;
using OrderCore.Api.Modules.Payments.Application.Contracts;
using OrderCore.Api.Modules.Payments.Application.Contracts.IntegrationEvents;
using OrderCore.Api.Modules.Payments.Application.DTOs;
using OrderCore.Api.Modules.Payments.Domain.Enums;
using OrderCore.Api.Modules.Payments.Domain.Repositories;

namespace OrderCore.Api.Modules.Payments.Application.UseCases;

/// <summary>
/// Returns the created <see cref="Domain.Entities.Refund"/> — 06-payments.md's
/// signature returns plain <c>Task</c>, but <c>PaymentsController.RequestRefundAsync</c>
/// has no other way to build a <c>RefundResponse</c> afterward: its field
/// list only has this use case, <c>CreatePaymentUseCase</c> and
/// <c>GetPaymentByOrderIdUseCase</c>, none of which can look a payment up
/// by its own id. Same class of gap as <c>ConfirmOrderUseCase</c> et al.
/// needing `now`.
/// </summary>
public sealed class RequestRefundUseCase
{
    private readonly IPaymentRepository _payments;
    private readonly IPaymentProvider _provider;
    private readonly IOutboxWriter _outbox;
    private readonly IAuditLogService _auditLog;
    private readonly TimeProvider _timeProvider;

    public RequestRefundUseCase(
        IPaymentRepository payments, IPaymentProvider provider, IOutboxWriter outbox, IAuditLogService auditLog, TimeProvider timeProvider)
    {
        _payments = payments;
        _provider = provider;
        _outbox = outbox;
        _auditLog = auditLog;
        _timeProvider = timeProvider;
    }

    public async Task<Domain.Entities.Refund> ExecuteAsync(RequestRefundCommand command, CancellationToken cancellationToken)
    {
        var payment = await _payments.GetByIdAsync(command.PaymentId, cancellationToken)
            ?? throw new InvalidOperationException($"Payment '{command.PaymentId}' was not found.");

        var now = _timeProvider.GetUtcNow();
        var refund = payment.RequestRefund(command.Amount, command.Reason, now);

        var result = await _provider.RefundAsync(payment, cancellationToken);
        var paymentFullyRefunded = false;

        if (result.Succeeded)
        {
            refund.Complete(_timeProvider.GetUtcNow());

            // Payment.Refund() marks the whole payment Refunded (there's no
            // PartiallyRefunded status — resolved decision, see
            // Docs/specs/payments/payment-processing.md), so only call it
            // once every completed refund adds up to the full amount;
            // otherwise a first partial refund would lock out any further
            // one (RequestRefund requires Status == Captured).
            var totalRefunded = payment.Refunds.Where(r => r.Status == RefundStatus.Completed).Sum(r => r.Amount);
            if (totalRefunded >= payment.Amount)
            {
                payment.Refund();
                paymentFullyRefunded = true;
            }

            _outbox.Enqueue(new PaymentRefunded
            {
                EventId = Guid.NewGuid(),
                Version = 1,
                OccurredAt = _timeProvider.GetUtcNow(),
                OrderId = payment.OrderId,
                PaymentId = payment.Id,
            });
        }
        else
        {
            refund.Fail(result.FailureReason ?? "unknown_failure", _timeProvider.GetUtcNow());
        }

        await _payments.SaveChangesAsync(cancellationToken);

        if (paymentFullyRefunded)
        {
            await _auditLog.RecordAsync(
                AuditLogActionNames.PaymentRefunded,
                "Payment",
                payment.Id,
                new Dictionary<string, string?> { ["amount"] = refund.Amount.ToString() },
                userId: null,
                cancellationToken);
        }

        return refund;
    }
}
