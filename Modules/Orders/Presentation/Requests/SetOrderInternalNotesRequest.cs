namespace OrderCore.Api.Modules.Orders.Presentation.Requests;

public sealed class SetOrderInternalNotesRequest
{
    /// <summary>Staff-only; never shown to the customer. Empty or null clears them.</summary>
    public string? Notes { get; init; }
}
