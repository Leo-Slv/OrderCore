namespace OrderCore.Api.Modules.Orders.Application.DTOs;

/// <summary>
/// What Orders needs to know about a Catalog product, in Orders' own terms,
/// so its Application layer never depends on Catalog's domain entity.
/// <see cref="IsPurchasable"/> is false for a product that isn't published
/// and active, which can't be ordered.
/// </summary>
public sealed record CatalogProductSnapshot(
    Guid Id,
    string Sku,
    string Slug,
    string Name,
    string? PrimaryImageUrl,
    decimal CurrentPrice,
    string Currency,
    bool IsPurchasable);
