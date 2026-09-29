using OrderCore.Api.Modules.Payments.Application.Contracts;
using OrderCore.Api.Modules.Payments.Application.DTOs;
using OrderCore.Api.Modules.Payments.Domain.Repositories;

namespace OrderCore.Api.Modules.Payments.Application.UseCases;

/// <summary>
/// What the buyer still has to do for an order's payment — for a checkout
/// replayed with the same key, which must hand the storefront the card
/// confirmation step again. The client secret isn't stored, so it is asked
/// of the provider again; null when the payment no longer waits for the buyer.
/// </summary>
public sealed class GetPaymentNextActionUseCase
{
    private readonly IPaymentRepository _payments;
    private readonly IPaymentProvider _provider;

    public GetPaymentNextActionUseCase(IPaymentRepository payments, IPaymentProvider provider)
    {
        _payments = payments;
        _provider = provider;
    }

    public async Task<PaymentNextAction?> ExecuteAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var payment = await _payments.GetByOrderIdAsync(orderId, cancellationToken);
        if (payment is not { IsAwaitingBuyer: true })
        {
            return null;
        }

        var clientSecret = await _provider.GetClientSecretAsync(payment, cancellationToken);
        return clientSecret is null ? null : PaymentNextAction.ConfirmCardWith(clientSecret);
    }
}
