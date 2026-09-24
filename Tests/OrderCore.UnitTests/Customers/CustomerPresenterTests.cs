using FluentAssertions;
using OrderCore.Api.Modules.Customers.Domain.Entities;
using OrderCore.Api.Modules.Customers.Presentation.Presenters;
using OrderCore.Api.Modules.Customers.Presentation.Responses;
using OrderCore.Api.Shared.Domain.ValueObjects;
using Xunit;

namespace OrderCore.UnitTests.Customers;

public sealed class CustomerPresenterTests
{
    [Fact]
    public void ToResponse_returns_the_full_address()
    {
        var address = CustomerAddress.Create(
            "Home", "Jane Doe", "+55 11 99999-0000",
            Address.Create("Main St", "123", "Apt 4", "Downtown", "Springfield", "IL", "62701", "USA"),
            DateTimeOffset.UtcNow);

        var response = CustomerPresenter.ToResponse(address);

        response.Should().BeEquivalentTo(new CustomerAddressResponse
        {
            Id = address.Id,
            Label = "Home",
            RecipientName = "Jane Doe",
            Phone = "+55 11 99999-0000",
            Street = "Main St",
            Number = "123",
            Complement = "Apt 4",
            Neighborhood = "Downtown",
            City = "Springfield",
            State = "IL",
            PostalCode = "62701",
            Country = "USA",
            IsDefaultShipping = address.IsDefaultShipping,
            IsDefaultBilling = address.IsDefaultBilling,
        });
    }
}
