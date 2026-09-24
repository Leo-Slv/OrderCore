using FluentAssertions;
using OrderCore.Api.Modules.Customers.Application.UseCases;
using OrderCore.Api.Modules.Customers.Domain.Entities;
using OrderCore.Api.Shared.Application.Exceptions;
using OrderCore.Api.Shared.Domain.Exceptions;
using OrderCore.Api.Shared.Domain.ValueObjects;
using Xunit;

namespace OrderCore.UnitTests.Customers;

public sealed class GetCustomerAddressUseCaseTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static async Task<(FakeCustomerRepository Repository, Customer Customer, CustomerAddress Address)> SetupAsync(
        string email = "jane@example.com")
    {
        var repository = new FakeCustomerRepository();
        var customer = Customer.Create("Jane Doe", email, "hashed-password", Now);
        var address = CustomerAddress.Create(
            "Home", "Jane Doe", "+55 11 99999-0000",
            Address.Create("Main St", "123", "Apt 4", "Downtown", "Springfield", "IL", "62701", "USA"), Now);
        customer.AddAddress(address);
        await repository.AddAsync(customer, CancellationToken.None);
        return (repository, customer, address);
    }

    [Fact]
    public async Task ExecuteAsync_returns_the_customers_address()
    {
        var (repository, customer, address) = await SetupAsync();

        var result = await new GetCustomerAddressUseCase(repository).ExecuteAsync(customer.Id, address.Id, CancellationToken.None);

        result.Id.Should().Be(address.Id);
        result.Address.Street.Should().Be("Main St");
    }

    [Fact]
    public async Task ExecuteAsync_for_unknown_customer_throws_customer_not_found()
    {
        var act = () => new GetCustomerAddressUseCase(new FakeCustomerRepository())
            .ExecuteAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>().Where(e => e.Code == "customer_not_found");
    }

    [Fact]
    public async Task ExecuteAsync_for_unknown_address_throws_address_not_found()
    {
        var (repository, customer, _) = await SetupAsync();

        var act = () => new GetCustomerAddressUseCase(repository).ExecuteAsync(customer.Id, Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>().Where(e => e.Code == "address_not_found");
    }

    [Fact]
    public async Task ExecuteAsync_does_not_hand_out_another_customers_address()
    {
        var (repository, _, someoneElsesAddress) = await SetupAsync();
        var other = Customer.Create("John Roe", "john@example.com", "hashed-password", Now);
        await repository.AddAsync(other, CancellationToken.None);

        var act = () => new GetCustomerAddressUseCase(repository).ExecuteAsync(other.Id, someoneElsesAddress.Id, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>().Where(e => e.Code == "address_not_found");
    }

    [Fact]
    public async Task ExecuteAsync_for_inactive_customer_throws_customer_inactive()
    {
        var (repository, customer, address) = await SetupAsync();
        customer.Deactivate();

        var act = () => new GetCustomerAddressUseCase(repository).ExecuteAsync(customer.Id, address.Id, CancellationToken.None);

        await act.Should().ThrowAsync<DomainRuleViolationException>().Where(e => e.Code == "customer_inactive");
    }
}
