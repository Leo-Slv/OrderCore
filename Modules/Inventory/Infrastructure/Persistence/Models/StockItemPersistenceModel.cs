namespace OrderCore.Api.Modules.Inventory.Infrastructure.Persistence.Models;

/// <summary>
/// Expanded beyond 04-inventory.md's abbreviated shape with
/// <see cref="Version"/> (concurrency token — the whole point of this
/// module's central guarantee, section 11) and <see cref="UpdatedAt"/>,
/// same reasoning as <c>CustomerPersistenceModel</c>.
/// </summary>
public sealed class StockItemPersistenceModel
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public Guid? ProductVariantId { get; set; }

    public int QuantityOnHand { get; set; }

    public int QuantityReserved { get; set; }

    public int ReorderLevel { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public int Version { get; set; }
}
