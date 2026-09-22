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

    Task AddAsync(Order order, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
