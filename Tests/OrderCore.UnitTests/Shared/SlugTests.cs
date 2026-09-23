using FluentAssertions;
using OrderCore.Api.Shared.Domain.ValueObjects;
using Xunit;

namespace OrderCore.UnitTests.Shared;

public sealed class SlugTests
{
    [Theory]
    [InlineData("widget")]
    [InlineData("wireless-mouse")]
    [InlineData("category-42")]
    public void Create_accepts_valid_slugs(string value)
    {
        var slug = Slug.Create(value);

        slug.Value.Should().Be(value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("Wireless-Mouse")]
    [InlineData("-leading-hyphen")]
    [InlineData("trailing-hyphen-")]
    [InlineData("double--hyphen")]
    [InlineData("with spaces")]
    public void Create_rejects_invalid_slugs(string value)
    {
        var act = () => Slug.Create(value);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Two_slugs_with_the_same_value_are_equal()
    {
        Slug.Create("widget").Should().Be(Slug.Create("widget"));
    }

    [Theory]
    [InlineData("Wireless Mouse", "wireless-mouse")]
    [InlineData("  Café Latte  ", "cafe-latte")]
    [InlineData("Men's T-Shirt (Blue)", "men-s-t-shirt-blue")]
    [InlineData("Consumer Electronics", "consumer-electronics")]
    public void GenerateFrom_derives_a_valid_slug_from_free_text(string text, string expected)
    {
        Slug.GenerateFrom(text).Value.Should().Be(expected);
    }

    [Fact]
    public void GenerateFrom_throws_when_text_has_no_usable_characters()
    {
        var act = () => Slug.GenerateFrom("!!!");

        act.Should().Throw<ArgumentException>();
    }
}
