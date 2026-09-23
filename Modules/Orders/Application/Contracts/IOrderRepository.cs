using OrderCore.Api.Modules.Orders.Domain.Entities;

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
    /// </summary>
    Task<IReadOnlyList<Order>> ListByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken);

    Task AddAsync(Order order, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
