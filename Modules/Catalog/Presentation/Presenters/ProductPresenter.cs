using System.Text.Json;
using OrderCore.Api.Modules.Catalog.Application.DTOs;
using OrderCore.Api.Modules.Catalog.Presentation.Requests;
using OrderCore.Api.Modules.Catalog.Presentation.Responses;
using OrderCore.Api.Shared.Application.DTOs;
using OrderCore.Api.Shared.Presentation.Responses;

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
        Slug = output.Slug,
        Name = output.Name,
        ShortDescription = output.ShortDescription,
        Description = output.Description,
        Brand = output.Brand,
        CategoryId = output.CategoryId,
        CurrentPrice = output.CurrentPrice,
        CompareAtPrice = output.CompareAtPrice,
        Currency = output.Currency,
        Status = output.Status.ToString(),
        Images = output.Images.Select(i => new ProductImageResponse
        {
            Id = i.Id,
            Url = i.Url,
            AltText = i.AltText,
            IsPrimary = i.IsPrimary,
            DisplayOrder = i.DisplayOrder,
        }).ToList(),
        Variants = output.Variants.Select(v => new ProductVariantResponse
        {
            Id = v.Id,
            Sku = v.Sku,
            Name = v.Name,
            Attributes = ParseAttributes(v.AttributesJson),
            AdditionalPrice = v.AdditionalPrice,
        }).ToList(),
        Availability = output.Availability.ToString(),
    };

    public static ProductSummaryResponse ToResponse(ProductSummaryOutput output) => new()
    {
        Id = output.Id,
        Sku = output.Sku,
        Slug = output.Slug,
        Name = output.Name,
        ShortDescription = output.ShortDescription,
        Brand = output.Brand,
        CategoryId = output.CategoryId,
        CurrentPrice = output.CurrentPrice,
        CompareAtPrice = output.CompareAtPrice,
        Currency = output.Currency,
        Status = output.Status.ToString(),
        PrimaryImageUrl = output.PrimaryImageUrl,
        Availability = output.Availability.ToString(),
    };

    public static PagedResponse<ProductSummaryResponse> ToResponse(PagedResult<ProductSummaryOutput> output) => new()
    {
        Items = output.Items.Select(ToResponse).ToList(),
        Page = output.Page,
        PageSize = output.PageSize,
        TotalItems = output.TotalItems,
        TotalPages = output.TotalPages,
    };

    /// <summary>
    /// <c>ProductVariant.AttributesJson</c> is free-form (the domain doesn't
    /// validate it), so anything that isn't a JSON object becomes an empty
    /// map instead of failing the whole product response. Non-string values
    /// keep their JSON text (<c>42</c>, <c>true</c>).
    /// </summary>
    private static IReadOnlyDictionary<string, string> ParseAttributes(string attributesJson)
    {
        if (string.IsNullOrWhiteSpace(attributesJson))
        {
            return new Dictionary<string, string>();
        }

        try
        {
            using var document = JsonDocument.Parse(attributesJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new Dictionary<string, string>();
            }

            return document.RootElement.EnumerateObject().ToDictionary(
                p => p.Name,
                p => p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString()! : p.Value.GetRawText());
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>();
        }
    }
}
