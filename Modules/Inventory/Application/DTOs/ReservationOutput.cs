using OrderCore.Api.Modules.Inventory.Domain.Entities;

namespace OrderCore.Api.Modules.Inventory.Application.DTOs;

public sealed record ReservationOutput(
    Guid Id,
    Guid ProductId,
    Guid OrderId,
    Guid OrderItemId,
    int Quantity,
    string Status,
    DateTimeOffset ReservedAt,
    DateTimeOffset? ReleasedAt,
    DateTimeOffset? ConsumedAt,
    DateTimeOffset? ReturnedAt)
{
    public static ReservationOutput From(InventoryReservation reservation) =>
        new(
            reservation.Id,
            reservation.ProductId,
            reservation.OrderId,
            reservation.OrderItemId,
            reservation.Quantity,
            reservation.Status.ToString(),
            reservation.ReservedAt,
            reservation.ReleasedAt,
            reservation.ConsumedAt,
            reservation.ReturnedAt);
}
