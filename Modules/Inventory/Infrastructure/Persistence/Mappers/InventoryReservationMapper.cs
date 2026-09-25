using OrderCore.Api.Modules.Inventory.Domain.Entities;
using OrderCore.Api.Modules.Inventory.Domain.Enums;
using OrderCore.Api.Modules.Inventory.Infrastructure.Persistence.Models;

namespace OrderCore.Api.Modules.Inventory.Infrastructure.Persistence.Mappers;

/// <summary>
/// Translates between the <see cref="InventoryReservation"/> aggregate and
/// its persistence model — see <c>StockItemMapper</c>'s remarks.
/// </summary>
public static class InventoryReservationMapper
{
    public static InventoryReservation ToDomain(InventoryReservationPersistenceModel model) => InventoryReservation.Rehydrate(
        model.Id,
        model.ProductId,
        model.OrderId,
        model.OrderItemId,
        model.Quantity,
        Enum.Parse<ReservationStatus>(model.Status),
        model.ReservedAt,
        model.ExpiresAt,
        model.ReleasedAt,
        model.ConsumedAt,
        model.ReturnedAt,
        model.Version);

    public static InventoryReservationPersistenceModel ToPersistence(InventoryReservation domain) => new()
    {
        Id = domain.Id,
        ProductId = domain.ProductId,
        OrderId = domain.OrderId,
        OrderItemId = domain.OrderItemId,
        Quantity = domain.Quantity,
        Status = domain.Status.ToString(),
        ReservedAt = domain.ReservedAt,
        ExpiresAt = domain.ExpiresAt,
        ReleasedAt = domain.ReleasedAt,
        ConsumedAt = domain.ConsumedAt,
        ReturnedAt = domain.ReturnedAt,
        Version = domain.Version,
    };

    public static void ApplyChanges(InventoryReservation domain, InventoryReservationPersistenceModel model)
    {
        model.Status = domain.Status.ToString();
        model.ExpiresAt = domain.ExpiresAt;
        model.ReleasedAt = domain.ReleasedAt;
        model.ConsumedAt = domain.ConsumedAt;
        model.ReturnedAt = domain.ReturnedAt;
        model.Version = domain.Version;
    }
}
