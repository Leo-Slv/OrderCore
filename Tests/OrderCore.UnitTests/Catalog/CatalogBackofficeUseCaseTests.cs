using FluentAssertions;
using OrderCore.Api.Modules.Catalog.Application.DTOs;
using OrderCore.Api.Modules.Catalog.Application.UseCases;
using OrderCore.Api.Modules.Catalog.Domain.Entities;
using OrderCore.Api.Modules.Catalog.Domain.Enums;
using OrderCore.Api.Shared.Application.Exceptions;
using OrderCore.Api.Shared.Domain.ValueObjects;
using Xunit;

namespace OrderCore.UnitTests.Catalog;

/// <summary>
/// Stock records follow the product (backoffice decision 6), the admin
/// list carries stock and filters by it (decision 5), and the product
/// operations answer 404 for an unknown product.
/// </summary>
public sealed class CatalogBackofficeUseCaseTests
{
    private readonly FakeProductRepository _products = new();
    private readonly FakeStockAvailabilityProvider _stock = new();

    private async Task<Product> SaveProductAsync(string sku, ProductStatus status = ProductStatus.Draft)
    {
        var product = Product.Create(sku, $"Product {sku}", Slug.Create(sku.ToLowerInvariant()), Guid.NewGuid(), 10m, "BRL", DateTimeOffset.UtcNow);
        if (status == ProductStatus.Active)
        {
            product.Publish(DateTimeOffset.UtcNow);
        }

        await _products.AddAsync(product, CancellationToken.None);
        return product;
    }

    [Fact]
    public async Task Creating_a_product_ensures_its_stock_record()
    {
        var categories = new FakeCategoryRepository();
        var category = Category.Create("Kitchen", Slug.Create("kitchen"), null, null, DateTimeOffset.UtcNow);
        await categories.AddAsync(category, CancellationToken.None);
        var useCase = new CreateProductUseCase(_products, _stock, _stock, categories, new FakeAuditLogService(), TimeProvider.System);

        var output = await useCase.ExecuteAsync(new CreateProductCommand("SKU-9", "Kettle", category.Id, 50m, "BRL"), CancellationToken.None);

        _stock.EnsuredProductIds.Should().Equal(output.Id);
    }

    [Fact]
    public async Task Publishing_a_product_ensures_its_stock_record_again()
    {
        var product = await SaveProductAsync("SKU-P");
        var useCase = new PublishProductUseCase(_products, _stock, _stock, new FakeAuditLogService(), TimeProvider.System);

        await useCase.ExecuteAsync(product.Id, CancellationToken.None);

        _stock.EnsuredProductIds.Should().Equal(product.Id);
    }

    [Fact]
    public async Task The_admin_list_includes_drafts_and_carries_each_products_stock()
    {
        var draft = await SaveProductAsync("SKU-D");
        var published = await SaveProductAsync("SKU-A", ProductStatus.Active);
        _stock.SetLevel(published.Id, new ProductStockLevel(5, 1, 4, 2, StockAvailability.InStock));

        var page = await new ListAdminProductsUseCase(_products, _stock).ExecuteAsync(new ListAdminProductsFilter(), CancellationToken.None);

        page.TotalItems.Should().Be(2);
        page.Items.Single(p => p.Id == published.Id).Stock!.QuantityAvailable.Should().Be(4);
        page.Items.Single(p => p.Id == draft.Id).Stock.Should().BeNull();
    }

    [Fact]
    public async Task Filtering_the_admin_list_by_stock_keeps_only_products_in_that_state()
    {
        var low = await SaveProductAsync("SKU-L");
        var plenty = await SaveProductAsync("SKU-X");
        _stock.SetLevel(low.Id, new ProductStockLevel(2, 0, 2, 5, StockAvailability.LowStock));
        _stock.SetLevel(plenty.Id, new ProductStockLevel(90, 0, 90, 5, StockAvailability.InStock));

        var page = await new ListAdminProductsUseCase(_products, _stock).ExecuteAsync(
            new ListAdminProductsFilter { Stock = StockAvailability.LowStock }, CancellationToken.None);

        page.Items.Should().ContainSingle().Which.Id.Should().Be(low.Id);
        page.TotalItems.Should().Be(1);
    }

    [Fact]
    public async Task The_admin_list_rejects_a_page_size_above_the_maximum()
    {
        var act = () => new ListAdminProductsUseCase(_products, _stock).ExecuteAsync(
            new ListAdminProductsFilter { PageSize = ListProductsFilter.MaximumPageSize + 1 }, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task A_promotion_on_an_unknown_product_is_not_found()
    {
        var act = () => new SetCompareAtPriceUseCase(_products, _stock, new FakeAuditLogService())
            .ExecuteAsync(Guid.NewGuid(), 99m, CancellationToken.None);

        (await act.Should().ThrowAsync<NotFoundException>()).Which.Code.Should().Be("product_not_found");
    }

    [Fact]
    public async Task Discontinuing_takes_the_product_off_sale()
    {
        var product = await SaveProductAsync("SKU-Z", ProductStatus.Active);

        var output = await new DiscontinueProductUseCase(_products, _stock, new FakeAuditLogService())
            .ExecuteAsync(product.Id, CancellationToken.None);

        output.Status.Should().Be(ProductStatus.Discontinued);
    }

    [Fact]
    public async Task Images_and_variants_are_managed_through_the_product()
    {
        var product = await SaveProductAsync("SKU-G");
        var images = new ManageProductImagesUseCase(_products, _stock, TimeProvider.System);
        var variants = new ManageProductVariantsUseCase(_products, _stock, TimeProvider.System);

        await images.AddAsync(product.Id, "https://example.com/a.png", "front", isPrimary: true, CancellationToken.None);
        var withTwo = await images.AddAsync(product.Id, "https://example.com/b.png", null, isPrimary: false, CancellationToken.None);
        var reordered = await images.ReorderAsync(product.Id, withTwo.Images.Select(i => i.Id).Reverse().ToList(), CancellationToken.None);
        var withVariant = await variants.AddAsync(product.Id, "SKU-G-M", "M", """{"size":"M"}""", 5m, CancellationToken.None);

        reordered.Images.Select(i => i.Url).Should().Equal("https://example.com/b.png", "https://example.com/a.png");
        withVariant.Variants.Should().ContainSingle().Which.AdditionalPrice.Should().Be(5m);
    }
}
