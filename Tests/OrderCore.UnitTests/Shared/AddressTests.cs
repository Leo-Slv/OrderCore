using FluentAssertions;
using OrderCore.Api.Shared.Domain.ValueObjects;
using Xunit;

namespace OrderCore.UnitTests.Shared;

public sealed class AddressTests
{
    [Fact]
    public void Create_with_blank_complement_normalizes_it_to_null()
    {
        var address = Address.Create(
            street: "Main St",
            number: "123",
            complement: "  ",
            neighborhood: "Downtown",
            city: "Springfield",
            state: "IL",
            postalCode: "62701",
            country: "USA");

        address.Complement.Should().BeNull();
    }

    [Theory]
    [InlineData("", "123", "Downtown", "Springfield", "IL", "62701", "USA")]
    [InlineData("Main St", "", "Downtown", "Springfield", "IL", "62701", "USA")]
    [InlineData("Main St", "123", "", "Springfield", "IL", "62701", "USA")]
    [InlineData("Main St", "123", "Downtown", "", "IL", "62701", "USA")]
    [InlineData("Main St", "123", "Downtown", "Springfield", "", "62701", "USA")]
    [InlineData("Main St", "123", "Downtown", "Springfield", "IL", "", "USA")]
    [InlineData("Main St", "123", "Downtown", "Springfield", "IL", "62701", "")]
    public void Create_rejects_blank_required_fields(
        string street, string number, string neighborhood, string city, string state, string postalCode, string country)
    {
        var act = () => Address.Create(street, number, complement: null, neighborhood, city, state, postalCode, country);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Two_addresses_with_the_same_values_are_equal()
    {
        var first = Address.Create("Main St", "123", null, "Downtown", "Springfield", "IL", "62701", "USA");
        var second = Address.Create("Main St", "123", null, "Downtown", "Springfield", "IL", "62701", "USA");

        first.Should().Be(second);
    }
}
