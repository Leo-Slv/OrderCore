using OrderCore.Api.Modules.Orders.Domain.Enums;

namespace OrderCore.Api.Modules.Orders.Application.DTOs;

/// <summary>
/// The backoffice dashboard for <c>[From, To)</c>, computed on demand from
/// each module's own data (backoffice decision 3):
/// <list type="bullet">
/// <item><see cref="OrdersByStatus"/>: orders created in the period, by current status
/// (every status is present, 0 when none);</item>
/// <item><see cref="RevenueByCurrency"/>: the total of orders confirmed in the period
/// that weren't cancelled afterwards (Confirmed, Processing, Shipped, Delivered);</item>
/// <item><see cref="NewCustomers"/>: customers who signed up in the period;</item>
/// <item><see cref="Stock"/>: low- and out-of-stock products right now, not per period;</item>
/// <item><see cref="RecentOrders"/>: the latest orders overall.</item>
/// </list>
/// </summary>
public sealed record DashboardOutput(
    DateTimeOffset From,
    DateTimeOffset To,
    IReadOnlyDictionary<OrderStatus, int> OrdersByStatus,
    IReadOnlyDictionary<string, decimal> RevenueByCurrency,
    int NewCustomers,
    StockAlertCounts Stock,
    IReadOnlyList<AdminOrderSummaryOutput> RecentOrders);
