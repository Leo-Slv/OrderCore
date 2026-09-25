using OrderCore.Api.Modules.Orders.Application.DTOs;
using OrderCore.Api.Modules.Orders.Domain.Entities;
using OrderCore.Api.Modules.Orders.Domain.Enums;

namespace OrderCore.Api.Modules.Orders.Application.Contracts;

/// <summary>
/// Persistence abstraction the Application layer depends on. The concrete
/// EF Core implementation lives in
/// Modules/Orders/Infrastructure/Persistence. Deliberately not a generic
/// <c>IRepository&lt;T&gt;</c> — see section 38: an abstraction here exists
/// because a real implementation swap and testability need exist, not
/// "because it might be useful".
/// </summary>
public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>
    /// Not in 05-orders.md's IOrderRepository — added because
    /// <c>ListCustomerOrdersUseCase</c> depends on this interface and has
    /// nothing else to query by; same class of gap as
    /// <c>IInventoryReservationRepository.ListByOrderIdAsync</c>.
    /// One page, newest order first, plus the customer's total order count.
    /// </summary>
    Task<(IReadOnlyList<Order> Items, int TotalCount)> ListByCustomerIdAsync(
        Guid customerId, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>
    /// The order a previous checkout with this key created for this
    /// customer, if any. See <c>CheckoutUseCase</c>.
    /// </summary>
    Task<Order?> FindByCheckoutIdempotencyKeyAsync(Guid customerId, string idempotencyKey, CancellationToken cancellationToken);

    /// <summary>The backoffice list: one page, newest first, plus the total. Read-only.</summary>
    Task<(IReadOnlyList<Order> Items, int TotalCount)> ListAsync(ListOrdersFilter filter, CancellationToken cancellationToken);

    /// <summary>Orders created in <c>[from, to)</c>, counted by their current status.</summary>
    Task<IReadOnlyDictionary<OrderStatus, int>> CountByStatusAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);

    /// <summary>
    /// Total amount, per currency, of the orders in <paramref name="statuses"/>
    /// that were confirmed in <c>[from, to)</c>.
    /// </summary>
    Task<IReadOnlyDictionary<string, decimal>> SumConfirmedTotalsAsync(
        DateTimeOffset from, DateTimeOffset to, IReadOnlyCollection<OrderStatus> statuses, CancellationToken cancellationToken);

    Task AddAsync(Order order, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
