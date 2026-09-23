using OrderCore.Api.Modules.Catalog.Application.DTOs;
using OrderCore.Api.Modules.Catalog.Presentation.Requests;
using OrderCore.Api.Modules.Catalog.Presentation.Responses;

namespace OrderCore.Api.Modules.Catalog.Presentation.Presenters;

public static class ProductPresenter
{
    public static CreateProductCommand ToCommand(CreateProductRequest request) => new(
        request.Sku, request.Name, request.CategoryId, request.CurrentPrice, request.Currency);

    public static UpdateProductCommand ToCommand(UpdateProductRequest request) => new(
        request.Name, request.ShortDescription, request.Description, request.Brand);

    public static ProductResponse ToResponse(ProductOutput output) => new()
    {
        Id = output.Id,
        Sku = output.Sku,
        Name = output.Name,
        CurrentPrice = output.CurrentPrice,
        CompareAtPrice = output.CompareAtPrice,
        Status = output.Status.ToString(),
        ImageUrls = output.ImageUrls,
    };
}
