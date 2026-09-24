namespace OrderCore.Api.Modules.Orders.Application.DTOs;

/// <summary>
/// The buyer's payment choice at checkout, owned by Orders so its API and
/// contracts don't expose Payments' own <c>PaymentMethod</c>.
/// <c>PaymentGatewayAdapter</c> maps between the two.
/// </summary>
public enum PaymentMethodChoice
{
    Card,
    Pix,
}
