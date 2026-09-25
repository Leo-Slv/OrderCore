namespace OrderCore.Api.Modules.Orders.Presentation.Responses;

/// <summary>
/// <c>GET admin/dashboard</c> for <c>[from, to)</c>.
/// <see cref="OrdersByStatus"/> counts orders created in the period by their
/// current status (every status present). <see cref="RevenueByCurrency"/>
/// sums orders confirmed in the period and not cancelled since.
/// <see cref="Stock"/> is now, not per period.
/// </summary>
public sealed class DashboardResponse
{
    public DateTimeOffset From { get; init; }

    public DateTimeOffset To { get; init; }

    public IReadOnlyDictionary<string, int> OrdersByStatus { get; init; } = new Dictionary<string, int>();

    public IReadOnlyDictionary<string, decimal> RevenueByCurrency { get; init; } = new Dictionary<string, decimal>();

    public int NewCustomers { get; init; }

    public DashboardStockResponse Stock { get; init; } = new();

    public IReadOnlyList<AdminOrderSummaryResponse> RecentOrders { get; init; } = [];
}
