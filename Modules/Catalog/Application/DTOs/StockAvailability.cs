namespace OrderCore.Api.Modules.Catalog.Application.DTOs;

/// <summary>
/// How much of a product's stock the storefront gets to know: a state,
/// never a quantity (resolved decision 2 in
/// Docs/specs/storefront/storefront-api-mvp.md). Owned by Catalog so its
/// API does not expose Inventory's own types.
/// </summary>
public enum StockAvailability
{
    InStock,
    LowStock,
    OutOfStock,
}
