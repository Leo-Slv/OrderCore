using OrderCore.Api.Modules.Inventory.Domain.Entities;

namespace OrderCore.Api.Modules.Inventory.Application.Contracts;

/// <summary>
/// No `SaveChangesAsync` — see <see cref="IUnitOfWork"/>'s remarks.
/// </summary>
public interface IInventoryReservationRepository
{
    Task<InventoryReservation?> GetByIdAsync(Guid reservationId, CancellationToken cancellationToken);

    Task<IReadOnlyList<InventoryReservation>> ListByOrderIdAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>One page of a product's reservations, newest first. Read-only.</summary>
    Task<(IReadOnlyList<InventoryReservation> Items, int TotalCount)> ListByProductIdAsync(
        Guid productId, int page, int pageSize, CancellationToken cancellationToken);

    Task AddAsync(InventoryReservation reservation, CancellationToken cancellationToken);
}
