using FluentAssertions;
using OrderCore.Api.Modules.Customers.Application.DTOs;
using OrderCore.Api.Modules.Customers.Application.UseCases;
using OrderCore.Api.Modules.Customers.Domain.Entities;
using OrderCore.Api.Shared.Application.Exceptions;
using OrderCore.Api.Shared.Domain.ValueObjects;
using Xunit;

namespace OrderCore.UnitTests.Customers;

/// <summary>Editing, removing and choosing defaults among a customer's own addresses.</summary>
public sealed class ManageCustomerAddressesUseCaseTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static Address SomeAddress(string street = "Main St") =>
        Address.Create(street, "123", null, "Downtown", "Springfield", "IL", "62701", "USA");

    private static async Task<(FakeCustomerRepository Repository, Customer Customer, CustomerAddress Home, CustomerAddress Work)> SetupAsync()
    {
        var repository = new FakeCustomerRepository();
        var customer = Customer.Create("Jane Doe", "jane@example.com", Now);
        var home = CustomerAddress.Create("Home", "Jane Doe", null, SomeAddress("Home St"), Now);
        var work = CustomerAddress.Create("Work", "Jane Doe", null, SomeAddress("Work Ave"), Now);
        customer.AddAddress(home);
        customer.AddAddress(work);
        await repository.AddAsync(customer, CancellationToken.None);
        return (repository, customer, home, work);
    }

    [Fact]
    public async Task UpdateAddress_replaces_the_editable_fields_and_keeps_the_id_and_default_flag()
    {
        var (repository, customer, home, _) = await SetupAsync();
        customer.SetDefaultShippingAddress(home.Id);

        var updated = await new UpdateCustomerAddressUseCase(repository, TimeProvider.System).ExecuteAsync(
            new UpdateCustomerAddressCommand(customer.Id, home.Id, "Old home", "John Doe", "+55 11 9999-0000", SomeAddress("New St")),
            CancellationToken.None);

        updated.Id.Should().Be(home.Id);
        updated.Label.Should().Be("Old home");
        updated.RecipientName.Should().Be("John Doe");
        updated.Address.Street.Should().Be("New St");
        updated.IsDefaultShipping.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveAddress_removes_only_that_address()
    {
        var (repository, customer, home, work) = await SetupAsync();

        await new RemoveCustomerAddressUseCase(repository).ExecuteAsync(customer.Id, home.Id, CancellationToken.None);

        customer.Addresses.Should().ContainSingle().Which.Id.Should().Be(work.Id);
    }

    [Fact]
    public async Task SetDefault_moves_the_default_to_the_chosen_address()
    {
        var (repository, customer, home, work) = await SetupAsync();
        var useCase = new SetDefaultAddressUseCase(repository);
        await useCase.ExecuteAsync(customer.Id, home.Id, DefaultAddressKind.Shipping, CancellationToken.None);

        await useCase.ExecuteAsync(customer.Id, work.Id, DefaultAddressKind.Shipping, CancellationToken.None);
        await useCase.ExecuteAsync(customer.Id, home.Id, DefaultAddressKind.Billing, CancellationToken.None);

        home.IsDefaultShipping.Should().BeFalse();
        work.IsDefaultShipping.Should().BeTrue();
        home.IsDefaultBilling.Should().BeTrue();
        work.IsDefaultBilling.Should().BeFalse();
    }

    [Fact]
    public async Task Someone_elses_address_is_not_found()
    {
        var (repository, _, home, _) = await SetupAsync();
        var other = Customer.Create("John Roe", "john@example.com", Now);
        await repository.AddAsync(other, CancellationToken.None);

        var remove = () => new RemoveCustomerAddressUseCase(repository).ExecuteAsync(other.Id, home.Id, CancellationToken.None);
        var setDefault = () => new SetDefaultAddressUseCase(repository)
            .ExecuteAsync(other.Id, home.Id, DefaultAddressKind.Billing, CancellationToken.None);
        var update = () => new UpdateCustomerAddressUseCase(repository, TimeProvider.System).ExecuteAsync(
            new UpdateCustomerAddressCommand(other.Id, home.Id, "Mine now", "John Roe", null, SomeAddress()), CancellationToken.None);

        await remove.Should().ThrowAsync<NotFoundException>().Where(e => e.Code == "address_not_found");
        await setDefault.Should().ThrowAsync<NotFoundException>().Where(e => e.Code == "address_not_found");
        await update.Should().ThrowAsync<NotFoundException>().Where(e => e.Code == "address_not_found");
    }

    [Fact]
    public async Task Unknown_customer_is_not_found()
    {
        var act = () => new RemoveCustomerAddressUseCase(new FakeCustomerRepository())
            .ExecuteAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>().Where(e => e.Code == "customer_not_found");
    }

    [Fact]
    public async Task UpdateAddress_validates_the_new_values()
    {
        var (repository, customer, home, _) = await SetupAsync();

        var act = () => new UpdateCustomerAddressUseCase(repository, TimeProvider.System).ExecuteAsync(
            new UpdateCustomerAddressCommand(customer.Id, home.Id, " ", "Jane Doe", null, SomeAddress()), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
