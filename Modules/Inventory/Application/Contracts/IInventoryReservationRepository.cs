using OrderCore.Api.Modules.Inventory.Domain.Entities;

namespace OrderCore.Api.Modules.Inventory.Application.Contracts;

/// <summary>
/// No `SaveChangesAsync` — see <see cref="IUnitOfWork"/>'s remarks.
/// </summary>
public interface IInventoryReservationRepository
{
    Task<InventoryReservation?> GetByIdAsync(Guid reservationId, CancellationToken cancellationToken);

    Task<IReadOnlyList<InventoryReservation>> ListByOrderIdAsync(Guid orderId, CancellationToken cancellationToken);

    Task AddAsync(InventoryReservation reservation, CancellationToken cancellationToken);
}
