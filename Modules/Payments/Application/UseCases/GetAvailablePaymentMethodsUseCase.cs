using OrderCore.Api.Modules.Payments.Application.DTOs;
using OrderCore.Api.Modules.Payments.Domain.Repositories;

namespace OrderCore.Api.Modules.Payments.Application.UseCases;

/// <summary>
/// The payment methods the configured provider takes (Docs/specs/payments/stripe-provider.md,
/// decision 7): the storefront shows only these, and checkout refuses the
/// others with <c>payment_method_unavailable</c>. With Stripe it also
/// carries the publishable key the Payment Element needs — never a secret.
/// </summary>
public sealed class GetAvailablePaymentMethodsUseCase
{
    private readonly IPaymentProvider _provider;

    public GetAvailablePaymentMethodsUseCase(IPaymentProvider provider)
    {
        _provider = provider;
    }

    public AvailablePaymentMethods Execute() =>
        new(_provider.Info.Name, _provider.Info.SupportedMethods.Order().ToList(), _provider.Info.PublishableKey);
}
