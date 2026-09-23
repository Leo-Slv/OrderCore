using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Modules.Customers.Domain.Entities;
using OrderCore.Api.Modules.Customers.Infrastructure.Persistence;
using OrderCore.Api.Modules.Customers.Infrastructure.Persistence.Repositories;
using OrderCore.Api.Shared.Domain.ValueObjects;
using Testcontainers.PostgreSql;
using Xunit;

namespace OrderCore.IntegrationTests.Customers;

/// <summary>
/// First real use of the Testcontainers.PostgreSql dependency reserved in
/// the csproj for "once OrderCore.Infrastructure has a real EF Core
/// implementation to test against" (section 33) — now that
/// EfCustomerRepository exists. Exercises the actual PostgreSQL provider
/// and the InitialCustomersSchema migration, not an in-memory provider,
/// since the Persistence Model / mapper round-trip and the concurrency
/// token are exactly the things an in-memory provider would paper over.
/// </summary>
public sealed class EfCustomerRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var options = new DbContextOptionsBuilder<CustomersDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        await using var dbContext = new CustomersDbContext(options);
        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private CustomersDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<CustomersDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options);

    private static Address SomeAddress() => Address.Create(
        "Main St", "123", null, "Downtown", "Springfield", "IL", "62701", "USA");

    [Fact]
    public async Task AddAsync_then_GetByIdAsync_round_trips_a_customer_with_an_address()
    {
        var customerId = Guid.Empty;

        await using (var dbContext = CreateDbContext())
        {
            var repository = new EfCustomerRepository(dbContext);
            var customer = Customer.Create("Jane Doe", "jane@example.com", "hashed-password", DateTimeOffset.UtcNow);
            var address = CustomerAddress.Create("Home", "Jane Doe", null, SomeAddress(), DateTimeOffset.UtcNow);
            customer.AddAddress(address);

            await repository.AddAsync(customer, CancellationToken.None);
            await repository.SaveChangesAsync(CancellationToken.None);
            customerId = customer.Id;
        }

        await using (var dbContext = CreateDbContext())
        {
            var repository = new EfCustomerRepository(dbContext);
            var reloaded = await repository.GetByIdAsync(customerId, CancellationToken.None);

            reloaded.Should().NotBeNull();
            reloaded!.Name.Should().Be("Jane Doe");
            reloaded.Addresses.Should().ContainSingle();
            reloaded.Addresses.Single().Address.City.Should().Be("Springfield");
        }
    }

    [Fact]
    public async Task Mutating_a_loaded_customer_and_saving_persists_the_change()
    {
        var customerId = Guid.Empty;

        await using (var dbContext = CreateDbContext())
        {
            var repository = new EfCustomerRepository(dbContext);
            var customer = Customer.Create("Jane Doe", "jane2@example.com", "hashed-password", DateTimeOffset.UtcNow);
            await repository.AddAsync(customer, CancellationToken.None);
            await repository.SaveChangesAsync(CancellationToken.None);
            customerId = customer.Id;
        }

        await using (var dbContext = CreateDbContext())
        {
            var repository = new EfCustomerRepository(dbContext);
            var customer = await repository.GetByIdAsync(customerId, CancellationToken.None);
            customer!.UpdateProfile("Jane R. Doe", "+1-555-0100", null);
            await repository.SaveChangesAsync(CancellationToken.None);
        }

        await using (var dbContext = CreateDbContext())
        {
            var repository = new EfCustomerRepository(dbContext);
            var reloaded = await repository.GetByIdAsync(customerId, CancellationToken.None);

            reloaded!.Name.Should().Be("Jane R. Doe");
            reloaded.Phone.Should().Be("+1-555-0100");
        }
    }

    [Fact]
    public async Task GetByEmailAsync_returns_null_for_an_unknown_email()
    {
        await using var dbContext = CreateDbContext();
        var repository = new EfCustomerRepository(dbContext);

        var result = await repository.GetByEmailAsync("nobody@example.com", CancellationToken.None);

        result.Should().BeNull();
    }
}
