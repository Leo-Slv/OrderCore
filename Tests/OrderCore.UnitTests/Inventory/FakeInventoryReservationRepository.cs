using OrderCore.Api.Modules.Inventory.Application.Contracts;
using OrderCore.Api.Modules.Inventory.Domain.Entities;

namespace OrderCore.UnitTests.Inventory;

internal sealed class FakeInventoryReservationRepository : IInventoryReservationRepository
{
    private readonly Dictionary<Guid, InventoryReservation> _reservations = new();

    public Task<InventoryReservation?> GetByIdAsync(Guid reservationId, CancellationToken cancellationToken) =>
        Task.FromResult(_reservations.GetValueOrDefault(reservationId));

    public Task<IReadOnlyList<InventoryReservation>> ListByOrderIdAsync(Guid orderId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<InventoryReservation>>(_reservations.Values.Where(r => r.OrderId == orderId).ToList());

    public Task AddAsync(InventoryReservation reservation, CancellationToken cancellationToken)
    {
        _reservations[reservation.Id] = reservation;
        return Task.CompletedTask;
    }
}
