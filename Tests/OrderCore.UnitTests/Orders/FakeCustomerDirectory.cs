using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Shared.Application.Exceptions;
using OrderCore.Api.Shared.Domain.ValueObjects;

namespace OrderCore.UnitTests.Orders;

internal sealed class FakeCustomerDirectory : ICustomerDirectory
{
    private readonly Dictionary<(Guid CustomerId, Guid AddressId), Address> _addresses = new();

    public Guid AddAddress(Guid customerId, string street = "Main St")
    {
        var addressId = Guid.NewGuid();
        _addresses[(customerId, addressId)] = Address.Create(street, "123", null, "Downtown", "Springfield", "IL", "62701", "USA");
        return addressId;
    }

    public Task<Address> GetAddressAsync(Guid customerId, Guid addressId, CancellationToken cancellationToken) =>
        _addresses.TryGetValue((customerId, addressId), out var address)
            ? Task.FromResult(address)
            : throw new NotFoundException("address_not_found", $"Address '{addressId}' was not found.");
}
