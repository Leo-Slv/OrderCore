using FluentAssertions;
using OrderCore.Api.Modules.Catalog.Application.DTOs;
using OrderCore.Api.Modules.Catalog.Domain.Enums;
using OrderCore.Api.Modules.Catalog.Presentation.Presenters;
using Xunit;

namespace OrderCore.UnitTests.Catalog;

public sealed class ProductPresenterTests
{
    private static ProductOutput OutputWithVariantAttributes(string attributesJson) => new(
        Guid.NewGuid(),
        "SKU-1",
        "wireless-mouse",
        "Wireless Mouse",
        ShortDescription: null,
        Description: null,
        Brand: null,
        Guid.NewGuid(),
        99.9m,
        CompareAtPrice: null,
        "BRL",
        ProductStatus.Active,
        Images: [],
        Variants: [new ProductVariantOutput(Guid.NewGuid(), "SKU-1-BLK", "Black", attributesJson, 0m)],
        StockAvailability.InStock);

    [Fact]
    public void ToResponse_turns_variant_attribute_json_into_a_name_value_map()
    {
        var response = ProductPresenter.ToResponse(OutputWithVariantAttributes("""{"color":"black","dpi":1600,"wireless":true}"""));

        response.Variants.Single().Attributes.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["color"] = "black",
            ["dpi"] = "1600",
            ["wireless"] = "true",
        });
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("""["black"]""")]
    public void ToResponse_uses_an_empty_map_for_attributes_that_are_not_a_json_object(string attributesJson)
    {
        var response = ProductPresenter.ToResponse(OutputWithVariantAttributes(attributesJson));

        response.Variants.Single().Attributes.Should().BeEmpty();
    }

    [Fact]
    public void ToResponse_writes_availability_and_status_by_name()
    {
        var response = ProductPresenter.ToResponse(OutputWithVariantAttributes("{}"));

        response.Availability.Should().Be("InStock");
        response.Status.Should().Be("Active");
    }
}
