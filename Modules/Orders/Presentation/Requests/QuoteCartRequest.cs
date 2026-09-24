namespace OrderCore.Api.Modules.Orders.Presentation.Requests;

public sealed class QuoteCartRequest
{
    public IReadOnlyList<QuoteCartLineRequest> Items { get; init; } = [];
}
