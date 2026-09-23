namespace OrderCore.Api.Modules.Inventory.Infrastructure.Persistence.Models;

/// <summary>
/// Expanded beyond 04-inventory.md's abbreviated shape with
/// <see cref="OrderItemId"/>, <see cref="ReservedAt"/>,
/// <see cref="ReleasedAt"/>, <see cref="ConsumedAt"/> and
/// <see cref="Version"/>, same reasoning as
/// <c>CustomerAddressPersistenceModel</c>.
/// </summary>
public sealed class InventoryReservationPersistenceModel
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public Guid OrderId { get; set; }

    public Guid OrderItemId { get; set; }

    public int Quantity { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset ReservedAt { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public DateTimeOffset? ReleasedAt { get; set; }

    public DateTimeOffset? ConsumedAt { get; set; }

    public int Version { get; set; }
}
