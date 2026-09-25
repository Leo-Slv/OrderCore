namespace OrderCore.Api.Modules.Orders.Presentation.Responses;

/// <summary>Low- and out-of-stock products right now.</summary>
public sealed class DashboardStockResponse
{
    public int LowStock { get; init; }

    public int OutOfStock { get; init; }
}
