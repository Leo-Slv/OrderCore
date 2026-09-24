using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Modules.Orders.Domain.Entities;

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
}
