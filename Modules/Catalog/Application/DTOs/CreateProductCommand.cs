namespace OrderCore.Api.Modules.Catalog.Application.DTOs;

/// <summary>
/// <see cref="Currency"/> is not in 03-catalog.md's command shape, but
/// <c>Product.Create</c> requires one — added the same way
/// <c>CreateOrderCommand.Currency</c> already is. There is no Slug field:
/// the use case derives one from <see cref="Name"/> via
/// <c>Slug.GenerateFrom</c>, matching <c>CreateCategoryCommand</c>.
/// </summary>
public sealed record CreateProductCommand(string Sku, string Name, Guid CategoryId, decimal CurrentPrice, string Currency);
