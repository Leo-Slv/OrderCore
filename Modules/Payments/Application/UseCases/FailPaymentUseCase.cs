using OrderCore.Api.Modules.AuditLogs.Application.Constants;
using OrderCore.Api.Modules.AuditLogs.Application.Services;
using OrderCore.Api.Modules.Payments.Application.Contracts;
using OrderCore.Api.Modules.Payments.Application.Contracts.IntegrationEvents;

namespace OrderCore.Api.Modules.Payments.Application.UseCases;

/// <summary>
/// Takes <see cref="TimeProvider"/> too — not in 06-payments.md's field
/// list, but <c>PaymentFailed.OccurredAt</c> needs a value (same class of
/// gap as <c>Customer.Create</c> gaining `now`).
/// </summary>
public sealed class FailPaymentUseCase
{
    private readonly IPaymentRepository _payments;
    private readonly IOutboxWriter _outbox;
    private readonly IAuditLogService _auditLog;
    private readonly TimeProvider _timeProvider;

    public FailPaymentUseCase(IPaymentRepository payments, IOutboxWriter outbox, IAuditLogService auditLog, TimeProvider timeProvider)
    {
        _payments = payments;
        _outbox = outbox;
        _auditLog = auditLog;
        _timeProvider = timeProvider;
    }

    public async Task ExecuteAsync(Guid paymentId, string reason, CancellationToken cancellationToken)
    {
        var payment = await _payments.GetByIdAsync(paymentId, cancellationToken)
            ?? throw new InvalidOperationException($"Payment '{paymentId}' was not found.");

        payment.Fail(reason);
        _outbox.Enqueue(new PaymentFailed
        {
            EventId = Guid.NewGuid(),
            Version = 1,
            OccurredAt = _timeProvider.GetUtcNow(),
            OrderId = payment.OrderId,
            PaymentId = payment.Id,
            Reason = reason,
        });

        await _payments.SaveChangesAsync(cancellationToken);

        await _auditLog.RecordAsync(
            AuditLogActionNames.PaymentFailed,
            "Payment",
            payment.Id,
            new Dictionary<string, string?> { ["orderId"] = payment.OrderId.ToString(), ["reason"] = reason },
            userId: null,
            cancellationToken);
    }
}
