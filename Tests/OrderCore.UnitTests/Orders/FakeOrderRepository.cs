using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Modules.Orders.Application.DTOs;
using OrderCore.Api.Modules.Orders.Domain.Entities;
using OrderCore.Api.Modules.Orders.Domain.Enums;

namespace OrderCore.UnitTests.Orders;

/// <summary>
/// In-memory stand-in for <see cref="IOrderRepository"/>, used by the use case
/// tests in this folder. Can be told to fail a save, to exercise
/// compensation paths.
/// </summary>
internal sealed class FakeOrderRepository : IOrderRepository
{
    private readonly Dictionary<Guid, Order> _orders = new();
    private readonly List<Order> _pending = new();

    public int SaveCount { get; private set; }

    /// <summary>Makes the next save fail, as a database error would.</summary>
    public Exception? FailNextSaveWith { get; set; }

    /// <summary>Runs just before the failing save throws, e.g. to simulate a concurrent writer.</summary>
    public Action? OnFailingSave { get; set; }

    public IReadOnlyCollection<Order> Orders => _orders.Values;

    public Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken) =>
        Task.FromResult(_orders.GetValueOrDefault(orderId));

    public Task<(IReadOnlyList<Order> Items, int TotalCount)> ListByCustomerIdAsync(
        Guid customerId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var matching = _orders.Values.Where(o => o.CustomerId == customerId).OrderByDescending(o => o.CreatedAt).ToList();
        IReadOnlyList<Order> pageItems = matching.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult((pageItems, matching.Count));
    }

    public Task<Order?> FindByCheckoutIdempotencyKeyAsync(Guid customerId, string idempotencyKey, CancellationToken cancellationToken) =>
        Task.FromResult(_orders.Values.FirstOrDefault(o => o.CustomerId == customerId && o.CheckoutIdempotencyKey == idempotencyKey));

    public Task AddAsync(Order order, CancellationToken cancellationToken)
    {
        _pending.Add(order);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        if (FailNextSaveWith is { } failure)
        {
            FailNextSaveWith = null;
            _pending.Clear();
            OnFailingSave?.Invoke();
            throw failure;
        }

        foreach (var order in _pending)
        {
            _orders[order.Id] = order;
        }

        _pending.Clear();
        SaveCount++;
        return Task.CompletedTask;
    }

    public void Store(Order order) => _orders[order.Id] = order;

    /// <summary>Applies the filter, newest first, and paging.</summary>
    public Task<(IReadOnlyList<Order> Items, int TotalCount)> ListAsync(ListOrdersFilter filter, CancellationToken cancellationToken)
    {
        var matching = _orders.Values
            .Where(o => filter.Status is null || o.Status == filter.Status)
            .Where(o => filter.CustomerId is null || o.CustomerId == filter.CustomerId)
            .Where(o => filter.CreatedFrom is null || o.CreatedAt >= filter.CreatedFrom)
            .Where(o => filter.CreatedTo is null || o.CreatedAt < filter.CreatedTo)
            .OrderByDescending(o => o.CreatedAt)
            .ToList();
        IReadOnlyList<Order> pageItems = matching.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToList();
        return Task.FromResult((pageItems, matching.Count));
    }

    public Task<IReadOnlyDictionary<OrderStatus, int>> CountByStatusAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<OrderStatus, int>>(Enum.GetValues<OrderStatus>().ToDictionary(
            s => s, s => _orders.Values.Count(o => o.Status == s && o.CreatedAt >= from && o.CreatedAt < to)));

    public Task<IReadOnlyDictionary<string, decimal>> SumConfirmedTotalsAsync(
        DateTimeOffset from, DateTimeOffset to, IReadOnlyCollection<OrderStatus> statuses, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<string, decimal>>(_orders.Values
            .Where(o => o.ConfirmedAt >= from && o.ConfirmedAt < to && statuses.Contains(o.Status))
            .GroupBy(o => o.Currency)
            .ToDictionary(g => g.Key, g => g.Sum(o => o.TotalAmount)));
}
