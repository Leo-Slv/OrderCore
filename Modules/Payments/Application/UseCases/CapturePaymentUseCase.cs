using OrderCore.Api.Modules.AuditLogs.Application.Constants;
using OrderCore.Api.Modules.AuditLogs.Application.Services;
using OrderCore.Api.Modules.Payments.Application.Contracts;
using OrderCore.Api.Modules.Payments.Application.DTOs;
using OrderCore.Api.Modules.Payments.Domain.Repositories;
using OrderCore.Api.Shared.Application.Exceptions;

namespace OrderCore.Api.Modules.Payments.Application.UseCases;

/// <summary>
/// Enqueues nothing: there is no <c>PaymentCaptured</c> integration event
/// (only PaymentRequested/Authorized/Failed/Refunded exist), so capture
/// stays a Payments-internal state change Orders never needs to react to
/// (resolved decision — see Docs/specs/payments/payment-processing.md).
/// </summary>
public sealed class CapturePaymentUseCase
{
    private readonly IPaymentRepository _payments;
    private readonly IPaymentProvider _provider;
    private readonly IAuditLogService _auditLog;
    private readonly TimeProvider _timeProvider;

    public CapturePaymentUseCase(IPaymentRepository payments, IPaymentProvider provider, IAuditLogService auditLog, TimeProvider timeProvider)
    {
        _payments = payments;
        _provider = provider;
        _auditLog = auditLog;
        _timeProvider = timeProvider;
    }

    public async Task<CreatePaymentResult> ExecuteAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        var payment = await _payments.GetByIdAsync(paymentId, cancellationToken)
            ?? throw new NotFoundException("payment_not_found", $"Payment '{paymentId}' was not found.");

        var result = await _provider.CaptureAsync(payment, cancellationToken);

        if (!result.Succeeded)
        {
            throw new ConflictException("payment_capture_failed", $"Capture failed for payment '{paymentId}': {result.FailureReason}.");
        }

        payment.Capture(_timeProvider.GetUtcNow());
        await _payments.SaveChangesAsync(cancellationToken);

        await _auditLog.RecordAsync(AuditLogActionNames.PaymentCaptured, "Payment", payment.Id, metadata: null, userId: null, cancellationToken);

        return new CreatePaymentResult(payment.Id, payment.Status.ToString());
    }
}
