namespace OrderCore.Api.Modules.Catalog.Application.DTOs;

/// <summary>
/// A product's stock as the backoffice sees it — admins get the numbers,
/// unlike the storefront's <see cref="StockAvailability"/> alone. Catalog's
/// own type, so its API doesn't expose Inventory's.
/// </summary>
public sealed record ProductStockLevel(
    int QuantityOnHand,
    int QuantityReserved,
    int QuantityAvailable,
    int ReorderLevel,
    StockAvailability State);
