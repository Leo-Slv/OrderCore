using OrderCore.Api.Modules.Inventory.Domain.Entities;
using OrderCore.Api.Modules.Inventory.Infrastructure.Persistence.Models;

namespace OrderCore.Api.Modules.Inventory.Infrastructure.Persistence.Mappers;

/// <summary>
/// Translates between the <see cref="StockItem"/> aggregate and its
/// persistence model — see <c>CustomerMapper</c>'s remarks on
/// <c>ToDomain</c> going through <c>Rehydrate</c>, not <c>Create</c>.
/// Also has an <c>ApplyChanges</c> the diagram doesn't list, for the same
/// reason <c>CategoryMapper</c> needed one.
/// </summary>
public static class StockItemMapper
{
    public static StockItem ToDomain(StockItemPersistenceModel model) => StockItem.Rehydrate(
        model.Id,
        model.ProductId,
        model.ProductVariantId,
        model.QuantityOnHand,
        model.QuantityReserved,
        model.ReorderLevel,
        model.UpdatedAt,
        model.Version);

    public static StockItemPersistenceModel ToPersistence(StockItem domain) => new()
    {
        Id = domain.Id,
        ProductId = domain.ProductId,
        ProductVariantId = domain.ProductVariantId,
        QuantityOnHand = domain.QuantityOnHand,
        QuantityReserved = domain.QuantityReserved,
        ReorderLevel = domain.ReorderLevel,
        UpdatedAt = domain.UpdatedAt,
        Version = domain.Version,
    };

    public static void ApplyChanges(StockItem domain, StockItemPersistenceModel model)
    {
        model.QuantityOnHand = domain.QuantityOnHand;
        model.QuantityReserved = domain.QuantityReserved;
        model.ReorderLevel = domain.ReorderLevel;
        model.UpdatedAt = domain.UpdatedAt;
        // Without this, EF Core's concurrency check on Version becomes a
        // no-op after the first update: the column would never actually
        // change, so every later writer's original-value check would keep
        // matching. This is the crux of section 11's guarantee — see
        // Docs/specs/inventory/stock-and-reservations.md.
        model.Version = domain.Version;
    }
}
