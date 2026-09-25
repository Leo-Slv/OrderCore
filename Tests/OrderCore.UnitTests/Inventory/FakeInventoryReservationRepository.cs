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

    public Task<(IReadOnlyList<InventoryReservation> Items, int TotalCount)> ListByProductIdAsync(
        Guid productId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var matching = _reservations.Values.Where(r => r.ProductId == productId).OrderByDescending(r => r.ReservedAt).ToList();
        return Task.FromResult<(IReadOnlyList<InventoryReservation>, int)>((matching.Skip((page - 1) * pageSize).Take(pageSize).ToList(), matching.Count));
    }

    public Task AddAsync(InventoryReservation reservation, CancellationToken cancellationToken)
    {
        _reservations[reservation.Id] = reservation;
        return Task.CompletedTask;
    }
}
