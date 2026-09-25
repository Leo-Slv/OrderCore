using OrderCore.Api.Modules.Catalog.Domain.Enums;

namespace OrderCore.Api.Modules.Catalog.Application.DTOs;

/// <summary>
/// The backoffice product list (and stock screen, backoffice decision 5):
/// drafts and discontinued products included, newest first.
/// </summary>
public sealed class ListAdminProductsFilter
{
    public ProductStatus? Status { get; init; }

    public Guid? CategoryId { get; init; }

    /// <summary>Matches the name or the SKU, case-insensitively.</summary>
    public string? SearchTerm { get; init; }

    /// <summary>Only products whose stock is in this state.</summary>
    public StockAvailability? Stock { get; init; }

    public int Page { get; init; } = ListProductsFilter.DefaultPage;

    public int PageSize { get; init; } = ListProductsFilter.DefaultPageSize;
}
