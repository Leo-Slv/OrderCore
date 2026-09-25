namespace OrderCore.Api.Modules.Orders.Presentation.Requests;

public sealed class CancelMyOrderRequest
{
    /// <summary>Why the customer is cancelling; optional (the admin sees "Cancelled by the customer" otherwise).</summary>
    public string? Reason { get; init; }
}
