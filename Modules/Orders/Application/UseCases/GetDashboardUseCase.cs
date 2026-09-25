using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Modules.Orders.Application.DTOs;
using OrderCore.Api.Modules.Orders.Domain.Enums;

namespace OrderCore.Api.Modules.Orders.Application.UseCases;

/// <summary>
/// The backoffice dashboard, computed on demand (backoffice decision 3):
/// each figure comes from the module that owns it — orders and revenue
/// from Orders, new customers from Customers, stock alerts from Inventory
/// — through Orders' existing contracts; nothing is kept in sync. Lives in
/// Orders because it is order-centric and Orders already reaches the other
/// two. The period defaults to the last 30 days.
/// </summary>
public sealed class GetDashboardUseCase
{
    public const int RecentOrderCount = 10;
    public static readonly TimeSpan DefaultPeriod = TimeSpan.FromDays(30);

    /// <summary>Orders whose money counts: confirmed and not cancelled since.</summary>
    private static readonly OrderStatus[] RevenueStatuses =
        [OrderStatus.Confirmed, OrderStatus.Processing, OrderStatus.Shipped, OrderStatus.Delivered];

    private readonly IOrderRepository _orderRepository;
    private readonly ICustomerDirectory _customerDirectory;
    private readonly IInventoryService _inventoryService;
    private readonly ListOrdersUseCase _listOrders;
    private readonly TimeProvider _timeProvider;

    public GetDashboardUseCase(
        IOrderRepository orderRepository,
        ICustomerDirectory customerDirectory,
        IInventoryService inventoryService,
        ListOrdersUseCase listOrders,
        TimeProvider timeProvider)
    {
        _orderRepository = orderRepository;
        _customerDirectory = customerDirectory;
        _inventoryService = inventoryService;
        _listOrders = listOrders;
        _timeProvider = timeProvider;
    }

    public async Task<DashboardOutput> ExecuteAsync(DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken)
    {
        var periodEnd = to ?? _timeProvider.GetUtcNow();
        var periodStart = from ?? periodEnd - DefaultPeriod;

        if (periodStart >= periodEnd)
        {
            throw new ArgumentException("The period must start before it ends.", nameof(from));
        }

        var ordersByStatus = await _orderRepository.CountByStatusAsync(periodStart, periodEnd, cancellationToken);
        var revenue = await _orderRepository.SumConfirmedTotalsAsync(periodStart, periodEnd, RevenueStatuses, cancellationToken);
        var newCustomers = await _customerDirectory.CountNewCustomersAsync(periodStart, periodEnd, cancellationToken);
        var stock = await _inventoryService.GetStockAlertCountsAsync(cancellationToken);
        var recent = await _listOrders.ExecuteAsync(new ListOrdersFilter { PageSize = RecentOrderCount }, cancellationToken);

        return new DashboardOutput(periodStart, periodEnd, ordersByStatus, revenue, newCustomers, stock, recent.Items);
    }
}
