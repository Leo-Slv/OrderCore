using FluentAssertions;
using OrderCore.Api.Modules.Catalog.Domain.Entities;
using OrderCore.Api.Modules.Catalog.Domain.Enums;
using OrderCore.Api.Modules.Catalog.Domain.Events;
using OrderCore.Api.Shared.Domain.ValueObjects;
using Xunit;

namespace OrderCore.UnitTests.Catalog;

public sealed class ProductTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static readonly Guid CategoryId = Guid.NewGuid();

    private static Product CreateProduct() =>
        Product.Create("SKU-1", "Widget", Slug.Create("widget"), CategoryId, 10m, "BRL", Now);

    [Fact]
    public void Create_starts_in_Draft_and_raises_ProductCreated()
    {
        var product = CreateProduct();

        product.Status.Should().Be(ProductStatus.Draft);
        product.Active.Should().BeTrue();
        product.DomainEvents.Should().ContainSingle(e => e is ProductCreated);
    }

    [Fact]
    public void ChangePrice_raises_ProductPriceChanged_with_old_and_new_price()
    {
        var product = CreateProduct();
        product.ClearDomainEvents();

        product.ChangePrice(15m, Now);

        product.CurrentPrice.Should().Be(15m);
        var raised = product.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ProductPriceChanged>().Subject;
        raised.OldPrice.Should().Be(10m);
        raised.NewPrice.Should().Be(15m);
    }

    [Fact]
    public void ChangePrice_rejects_a_negative_price()
    {
        var product = CreateProduct();

        var act = () => product.ChangePrice(-1m, Now);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Publish_from_Draft_sets_PublishedAt_and_raises_ProductPublished()
    {
        var product = CreateProduct();
        product.ClearDomainEvents();

        product.Publish(Now);

        product.Status.Should().Be(ProductStatus.Active);
        product.PublishedAt.Should().Be(Now);
        product.DomainEvents.Should().ContainSingle(e => e is ProductPublished);
    }

    [Fact]
    public void Publish_twice_throws()
    {
        var product = CreateProduct();
        product.Publish(Now);

        var act = () => product.Publish(Now);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Discontinue_twice_throws()
    {
        var product = CreateProduct();
        product.Discontinue();

        var act = () => product.Discontinue();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddImage_as_primary_unmarks_the_previous_primary_image()
    {
        var product = CreateProduct();
        product.AddImage("https://example.com/1.png", null, isPrimary: true, Now);
        product.AddImage("https://example.com/2.png", null, isPrimary: true, Now);

        var images = product.Images.ToList();
        images[0].IsPrimary.Should().BeFalse();
        images[1].IsPrimary.Should().BeTrue();
    }

    [Fact]
    public void RemoveImage_for_unknown_id_throws()
    {
        var product = CreateProduct();

        var act = () => product.RemoveImage(Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ReorderImages_updates_DisplayOrder_to_match_the_given_sequence()
    {
        var product = CreateProduct();
        product.AddImage("https://example.com/1.png", null, isPrimary: false, Now);
        product.AddImage("https://example.com/2.png", null, isPrimary: false, Now);
        var images = product.Images.ToList();

        product.ReorderImages([images[1].Id, images[0].Id]);

        images[1].DisplayOrder.Should().Be(0);
        images[0].DisplayOrder.Should().Be(1);
    }

    [Fact]
    public void AddVariant_then_RemoveVariant_round_trips()
    {
        var product = CreateProduct();

        product.AddVariant("SKU-1-RED", "Red", "{\"color\":\"red\"}", 2m, Now);
        product.Variants.Should().ContainSingle();

        product.RemoveVariant(product.Variants.Single().Id);
        product.Variants.Should().BeEmpty();
    }
}
