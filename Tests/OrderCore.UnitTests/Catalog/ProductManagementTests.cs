using FluentAssertions;
using OrderCore.Api.Modules.Catalog.Domain.Entities;
using OrderCore.Api.Shared.Domain.Exceptions;
using OrderCore.Api.Shared.Domain.ValueObjects;
using Xunit;

namespace OrderCore.UnitTests.Catalog;

/// <summary>
/// The product rules the backoffice endpoints rely on: promotions, image
/// order, and input that would otherwise only fail in the database.
/// </summary>
public sealed class ProductManagementTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static Product CreateProduct(decimal price = 100m) =>
        Product.Create("SKU-1", "Widget", Slug.Create("widget"), Guid.NewGuid(), price, "BRL", Now);

    [Fact]
    public void A_compare_at_price_above_the_price_starts_a_promotion_and_null_ends_it()
    {
        var product = CreateProduct(price: 80m);

        product.SetCompareAtPrice(100m);
        product.CompareAtPrice.Should().Be(100m);

        product.SetCompareAtPrice(null);
        product.CompareAtPrice.Should().BeNull();
    }

    [Theory]
    [InlineData(80)]
    [InlineData(50)]
    public void A_compare_at_price_not_above_the_price_is_rejected(decimal compareAtPrice)
    {
        var product = CreateProduct(price: 80m);

        var act = () => product.SetCompareAtPrice(compareAtPrice);

        act.Should().Throw<DomainRuleViolationException>().Which.Code.Should().Be("invalid_compare_at_price");
    }

    [Fact]
    public void Raising_the_price_to_the_compare_at_price_ends_the_promotion()
    {
        var product = CreateProduct(price: 80m);
        product.SetCompareAtPrice(100m);

        product.ChangePrice(100m, Now);

        product.CompareAtPrice.Should().BeNull();
    }

    [Fact]
    public void Lowering_the_price_keeps_the_promotion()
    {
        var product = CreateProduct(price: 80m);
        product.SetCompareAtPrice(100m);

        product.ChangePrice(70m, Now);

        product.CompareAtPrice.Should().Be(100m);
    }

    [Fact]
    public void New_images_are_appended_after_the_existing_ones()
    {
        var product = CreateProduct();

        product.AddImage("https://example.com/1.png", null, isPrimary: false, Now);
        product.AddImage("https://example.com/2.png", null, isPrimary: false, Now);

        product.Images.OrderBy(i => i.DisplayOrder).Select(i => i.Url)
            .Should().Equal("https://example.com/1.png", "https://example.com/2.png");
        product.Images.Select(i => i.DisplayOrder).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Reordering_must_list_every_image_exactly_once()
    {
        var product = CreateProduct();
        product.AddImage("https://example.com/1.png", null, isPrimary: false, Now);
        product.AddImage("https://example.com/2.png", null, isPrimary: false, Now);
        var first = product.Images.First().Id;

        product.Invoking(p => p.ReorderImages([first]))
            .Should().Throw<DomainRuleViolationException>().Which.Code.Should().Be("invalid_image_order");
        product.Invoking(p => p.ReorderImages([first, first]))
            .Should().Throw<DomainRuleViolationException>().Which.Code.Should().Be("invalid_image_order");
    }

    [Theory]
    [InlineData("images/1.png")]
    [InlineData("ftp://example.com/1.png")]
    public void An_image_must_have_an_absolute_http_address(string url)
    {
        var act = () => CreateProduct().AddImage(url, null, isPrimary: false, Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Two_variants_of_a_product_cannot_share_a_sku()
    {
        var product = CreateProduct();
        product.AddVariant("SKU-1-M", "M", """{"size":"M"}""", 0m, Now);

        var act = () => product.AddVariant("sku-1-m", "Medium", """{"size":"M"}""", 0m, Now);

        act.Should().Throw<DomainRuleViolationException>().Which.Code.Should().Be("variant_sku_already_exists");
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[1, 2]")]
    [InlineData("")]
    public void Variant_attributes_must_be_a_json_object(string attributesJson)
    {
        var act = () => CreateProduct().AddVariant("SKU-1-M", "M", attributesJson, 0m, Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_variant_sku_longer_than_the_column_is_rejected()
    {
        var act = () => CreateProduct().AddVariant(new string('X', ProductVariant.MaxSkuLength + 1), "M", "{}", 0m, Now);

        act.Should().Throw<ArgumentException>();
    }
}
