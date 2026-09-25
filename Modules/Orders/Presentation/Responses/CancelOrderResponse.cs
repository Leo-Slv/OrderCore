namespace OrderCore.Api.Modules.Orders.Presentation.Responses;

/// <summary>
/// What cancelling did to the payment: <c>NothingToSettle</c> (none, or it
/// never took money), <c>Voided</c> (the authorization was released) or
/// <c>Refunded</c>.
/// </summary>
public sealed class CancelOrderResponse
{
    public string PaymentSettlement { get; init; } = string.Empty;
}
