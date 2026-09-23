namespace OrderCore.Api.Modules.Orders.Presentation.Requests;

public sealed class CancelOrderRequest
{
    public string Reason { get; init; } = string.Empty;
}
