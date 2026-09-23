using OrderCore.Api.Modules.Catalog.Domain.Entities;
using OrderCore.Api.Modules.Catalog.Domain.Enums;
using OrderCore.Api.Modules.Catalog.Infrastructure.Persistence.Models;
using OrderCore.Api.Shared.Domain.ValueObjects;
using OrderCore.Api.Shared.Infrastructure.Persistence;

namespace OrderCore.Api.Modules.Catalog.Infrastructure.Persistence.Mappers;

/// <summary>
/// Translates between the <see cref="Product"/> aggregate and its
/// persistence models — see <c>CustomerMapper</c>'s remarks on
/// <c>ToDomain</c> going through <c>Rehydrate</c>, not <c>Create</c>.
/// </summary>
public static class ProductMapper
{
    public static Product ToDomain(ProductPersistenceModel model)
    {
        var images = model.Images.Select(ToDomain);
        var variants = model.Variants.Select(ToDomain);

        return Product.Rehydrate(
            model.Id,
            model.Sku,
            model.Name,
            Slug.Create(model.Slug),
            model.ShortDescription,
            model.Description,
            model.CategoryId,
            model.Brand,
            model.CurrentPrice,
            model.CompareAtPrice,
            model.Currency,
            model.WeightGrams,
            model.HeightCm,
            model.WidthCm,
            model.DepthCm,
            Enum.Parse<ProductStatus>(model.Status),
            model.Active,
            model.PublishedAt,
            model.CreatedAt,
            model.UpdatedAt,
            model.Version,
            images,
            variants);
    }

    public static ProductPersistenceModel ToPersistence(Product domain) => new()
    {
        Id = domain.Id,
        Sku = domain.Sku,
        Name = domain.Name,
        Slug = domain.Slug.Value,
        ShortDescription = domain.ShortDescription,
        Description = domain.Description,
        CategoryId = domain.CategoryId,
        Brand = domain.Brand,
        CurrentPrice = domain.CurrentPrice,
        CompareAtPrice = domain.CompareAtPrice,
        Currency = domain.Currency,
        WeightGrams = domain.WeightGrams,
        HeightCm = domain.HeightCm,
        WidthCm = domain.WidthCm,
        DepthCm = domain.DepthCm,
        Status = domain.Status.ToString(),
        Active = domain.Active,
        PublishedAt = domain.PublishedAt,
        CreatedAt = domain.CreatedAt,
        UpdatedAt = domain.UpdatedAt,
        Version = domain.Version,
        Images = domain.Images.Select(ToPersistence).ToList(),
        Variants = domain.Variants.Select(ToPersistence).ToList(),
    };

    /// <summary>
    /// Applies the current state of an already-tracked <paramref name="domain"/>
    /// aggregate onto its persistence model in place — see
    /// <c>CustomerMapper.ApplyChanges</c>.
    /// </summary>
    public static void ApplyChanges(Product domain, ProductPersistenceModel model)
    {
        model.Name = domain.Name;
        model.ShortDescription = domain.ShortDescription;
        model.Description = domain.Description;
        model.Brand = domain.Brand;
        model.CurrentPrice = domain.CurrentPrice;
        model.CompareAtPrice = domain.CompareAtPrice;
        model.WeightGrams = domain.WeightGrams;
        model.HeightCm = domain.HeightCm;
        model.WidthCm = domain.WidthCm;
        model.DepthCm = domain.DepthCm;
        model.Status = domain.Status.ToString();
        model.Active = domain.Active;
        model.PublishedAt = domain.PublishedAt;
        model.UpdatedAt = domain.UpdatedAt;
        model.Version = domain.Version;

        ChildCollectionReconciler.Reconcile(domain.Images, model.Images, ToPersistence, ApplyChanges, i => i.Id);
        ChildCollectionReconciler.Reconcile(domain.Variants, model.Variants, ToPersistence, ApplyChanges, v => v.Id);
    }

    private static ProductImagePersistenceModel ToPersistence(ProductImage domain) => new()
    {
        Id = domain.Id,
        Url = domain.Url,
        AltText = domain.AltText,
        DisplayOrder = domain.DisplayOrder,
        IsPrimary = domain.IsPrimary,
        CreatedAt = domain.CreatedAt,
    };

    private static void ApplyChanges(ProductImage domain, ProductImagePersistenceModel model)
    {
        model.Url = domain.Url;
        model.AltText = domain.AltText;
        model.DisplayOrder = domain.DisplayOrder;
        model.IsPrimary = domain.IsPrimary;
    }

    private static ProductImage ToDomain(ProductImagePersistenceModel model) => ProductImage.Rehydrate(
        model.Id, model.Url, model.AltText, model.DisplayOrder, model.IsPrimary, model.CreatedAt);

    private static ProductVariantPersistenceModel ToPersistence(ProductVariant domain) => new()
    {
        Id = domain.Id,
        Sku = domain.Sku,
        Name = domain.Name,
        AttributesJson = domain.AttributesJson,
        AdditionalPrice = domain.AdditionalPrice,
        Active = domain.Active,
        CreatedAt = domain.CreatedAt,
        UpdatedAt = domain.UpdatedAt,
    };

    private static void ApplyChanges(ProductVariant domain, ProductVariantPersistenceModel model)
    {
        model.Name = domain.Name;
        model.AttributesJson = domain.AttributesJson;
        model.AdditionalPrice = domain.AdditionalPrice;
        model.Active = domain.Active;
        model.UpdatedAt = domain.UpdatedAt;
    }

    private static ProductVariant ToDomain(ProductVariantPersistenceModel model) => ProductVariant.Rehydrate(
        model.Id, model.Sku, model.Name, model.AttributesJson, model.AdditionalPrice, model.Active, model.CreatedAt, model.UpdatedAt);
}
