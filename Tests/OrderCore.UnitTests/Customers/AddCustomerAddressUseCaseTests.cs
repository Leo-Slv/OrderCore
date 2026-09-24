using FluentAssertions;
using OrderCore.Api.Modules.Customers.Application.DTOs;
using OrderCore.Api.Modules.Customers.Application.UseCases;
using OrderCore.Api.Modules.Customers.Domain.Entities;
using OrderCore.Api.Shared.Application.Exceptions;
using OrderCore.Api.Shared.Domain.ValueObjects;
using Xunit;

namespace OrderCore.UnitTests.Customers;

public sealed class AddCustomerAddressUseCaseTests
{
    private static Address SomeAddress() => Address.Create(
        "Main St", "123", null, "Downtown", "Springfield", "IL", "62701", "USA");

    [Fact]
    public async Task ExecuteAsync_adds_an_address_to_an_existing_customer()
    {
        var repository = new FakeCustomerRepository();
        var customer = Customer.Create("Jane Doe", "jane@example.com", DateTimeOffset.UtcNow);
        await repository.AddAsync(customer, CancellationToken.None);
        var useCase = new AddCustomerAddressUseCase(repository, TimeProvider.System);

        var command = new AddCustomerAddressCommand(customer.Id, "Home", "Jane Doe", Phone: null, SomeAddress());
        var addressId = await useCase.ExecuteAsync(command, CancellationToken.None);

        var reloaded = await repository.GetByIdAsync(customer.Id, CancellationToken.None);
        reloaded!.Addresses.Should().ContainSingle(a => a.Id == addressId);
    }

    [Fact]
    public async Task ExecuteAsync_for_unknown_customer_throws()
    {
        var useCase = new AddCustomerAddressUseCase(new FakeCustomerRepository(), TimeProvider.System);
        var command = new AddCustomerAddressCommand(Guid.NewGuid(), "Home", "Jane Doe", Phone: null, SomeAddress());

        var act = () => useCase.ExecuteAsync(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>().Where(e => e.Code == "customer_not_found");
    }
}
