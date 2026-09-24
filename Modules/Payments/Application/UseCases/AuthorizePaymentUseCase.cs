using OrderCore.Api.Modules.Payments.Application.Contracts;
using OrderCore.Api.Modules.Payments.Application.Contracts.IntegrationEvents;
using OrderCore.Api.Modules.Payments.Application.DTOs;
using OrderCore.Api.Modules.Payments.Domain.Enums;
using OrderCore.Api.Modules.Payments.Domain.Repositories;

namespace OrderCore.Api.Modules.Payments.Application.UseCases;

/// <summary>
/// Explicit retry path for a payment stuck in <c>Pending</c>/<c>Processing</c>
/// — e.g. the provider call inside <see cref="CreatePaymentUseCase"/> threw
/// before an outcome could be recorded. Not called by
/// <see cref="CreatePaymentUseCase"/> itself (resolved decision — see
/// Docs/specs/payments/payment-processing.md).
/// </summary>
public sealed class AuthorizePaymentUseCase
{
    private readonly IPaymentRepository _payments;
    private readonly IPaymentProvider _provider;
    private readonly IOutboxWriter _outbox;
    private readonly TimeProvider _timeProvider;

    public AuthorizePaymentUseCase(IPaymentRepository payments, IPaymentProvider provider, IOutboxWriter outbox, TimeProvider timeProvider)
    {
        _payments = payments;
        _provider = provider;
        _outbox = outbox;
        _timeProvider = timeProvider;
    }

    public async Task<CreatePaymentResult> ExecuteAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        var payment = await _payments.GetByIdAsync(paymentId, cancellationToken)
            ?? throw new InvalidOperationException($"Payment '{paymentId}' was not found.");

        if (payment.Status == PaymentStatus.Pending)
        {
            payment.MarkProcessing();
        }

        var result = await _provider.AuthorizeAsync(payment, cancellationToken);

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

        return new CreatePaymentResult(payment.Id, payment.Status.ToString());
    }
}
