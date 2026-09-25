using OrderCore.Api.Modules.Catalog.Application.DTOs;
using OrderCore.Api.Modules.Catalog.Presentation.Responses;
using OrderCore.Api.Shared.Application.DTOs;
using OrderCore.Api.Shared.Presentation.Responses;

namespace OrderCore.Api.Modules.Catalog.Presentation.Presenters;

public static class AdminProductPresenter
{
    public static AdminProductSummaryResponse ToResponse(AdminProductSummaryOutput output) => new()
    {
        Id = output.Id,
        Sku = output.Sku,
        Slug = output.Slug,
        Name = output.Name,
        CategoryId = output.CategoryId,
        CurrentPrice = output.CurrentPrice,
        CompareAtPrice = output.CompareAtPrice,
        Currency = output.Currency,
        Status = output.Status.ToString(),
        Active = output.Active,
        PrimaryImageUrl = output.PrimaryImageUrl,
        CreatedAt = output.CreatedAt,
        PublishedAt = output.PublishedAt,
        Stock = output.Stock is { } stock
            ? new ProductStockLevelResponse
            {
                QuantityOnHand = stock.QuantityOnHand,
                QuantityReserved = stock.QuantityReserved,
                QuantityAvailable = stock.QuantityAvailable,
                ReorderLevel = stock.ReorderLevel,
                State = stock.State.ToString(),
            }
            : null,
    };

    public static PagedResponse<AdminProductSummaryResponse> ToResponse(PagedResult<AdminProductSummaryOutput> output) => new()
    {
        Items = output.Items.Select(ToResponse).ToList(),
        Page = output.Page,
        PageSize = output.PageSize,
        TotalItems = output.TotalItems,
        TotalPages = output.TotalPages,
    };
}
