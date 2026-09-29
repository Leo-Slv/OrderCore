using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Modules.Orders.Application.DTOs;
using OrderCore.Api.Shared.Domain.Exceptions;

namespace OrderCore.Api.Modules.Orders.Application.UseCases;

/// <summary>
/// Refuses a payment method the configured provider doesn't take (Pix under
/// Stripe) before anything happens — no stock reserved, no order saved.
/// Payments checks it again when the payment is created.
/// </summary>
internal static class PaymentMethodAvailability
{
    public static void Ensure(IPaymentGateway paymentGateway, PaymentMethodChoice method)
    {
        if (!paymentGateway.GetAvailableMethods().Contains(method))
        {
            throw new DomainRuleViolationException("payment_method_unavailable", $"{method} payments are not available right now.");
        }
    }
}
