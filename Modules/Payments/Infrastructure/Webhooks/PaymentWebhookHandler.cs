using System.Text.Json;
using OrderCore.Api.Modules.Payments.Application.Contracts;
using OrderCore.Api.Modules.Payments.Application.UseCases;
using OrderCore.Api.Shared.Application.Exceptions;

namespace OrderCore.Api.Modules.Payments.Infrastructure.Webhooks;

/// <summary>
/// Entry point a real provider's async callback would hit. No public HTTP
/// endpoint routes to it in 06-payments.md's `PaymentsController` — today
/// it's invoked directly (by tests/manual calls), ready to be wired to an
/// actual webhook endpoint once a real provider (Stripe) needs one (see
/// Docs/specs/payments/payment-processing.md). Takes a
/// <see cref="CancellationToken"/> too, not in the diagram's signature —
/// same convention as every other async method in the project.
/// </summary>
public sealed class PaymentWebhookHandler
{
    private readonly IPaymentRepository _payments;
    private readonly CapturePaymentUseCase _capturePayment;
    private readonly FailPaymentUseCase _failPayment;

    public PaymentWebhookHandler(IPaymentRepository payments, CapturePaymentUseCase capturePayment, FailPaymentUseCase failPayment)
    {
        _payments = payments;
        _capturePayment = capturePayment;
        _failPayment = failPayment;
    }

    public async Task HandleAsync(string providerEventType, string payloadJson, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<PaymentWebhookPayload>(payloadJson)
            ?? throw new ArgumentException("Invalid webhook payload.", nameof(payloadJson));

        var payment = await _payments.GetByIdAsync(payload.PaymentId, cancellationToken)
            ?? throw new NotFoundException("payment_not_found", $"Payment '{payload.PaymentId}' was not found.");

        switch (providerEventType)
        {
            case "payment.captured":
                await _capturePayment.ExecuteAsync(payment.Id, cancellationToken);
                break;
            case "payment.failed":
                await _failPayment.ExecuteAsync(payment.Id, payload.Reason ?? "provider_reported_failure", cancellationToken);
                break;
            default:
                throw new InvalidOperationException($"Unsupported webhook event type '{providerEventType}'.");
        }
    }

    private sealed record PaymentWebhookPayload(Guid PaymentId, string? Reason);
}
