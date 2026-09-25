using FluentAssertions;
using OrderCore.Api.Modules.Customers.Application.DTOs;
using OrderCore.Api.Modules.Customers.Application.UseCases;
using OrderCore.Api.Modules.Customers.Domain.Entities;
using OrderCore.Api.Shared.Application.Exceptions;
using Xunit;

namespace OrderCore.UnitTests.Customers;

/// <summary>
/// The backoffice side of Customers: the list with search, deactivating
/// and reactivating (idempotent, audited once), and the reads other
/// modules use.
/// </summary>
public sealed class CustomerBackofficeUseCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeCustomerRepository _customers = new();
    private readonly FakeAuditLogService _auditLog = new();

    private async Task<Customer> SaveCustomerAsync(string name, string email, DateTimeOffset createdAt)
    {
        var customer = Customer.Create(name, email, createdAt);
        await _customers.AddAsync(customer, CancellationToken.None);
        return customer;
    }

    [Fact]
    public async Task The_list_searches_name_and_email_and_is_newest_first()
    {
        await SaveCustomerAsync("Ana Souza", "ana@example.com", Now.AddDays(-2));
        await SaveCustomerAsync("Bruno Lima", "bruno@souza.dev", Now.AddDays(-1));
        await SaveCustomerAsync("Carla Dias", "carla@example.com", Now);

        var page = await new ListCustomersUseCase(_customers).ExecuteAsync(new ListCustomersFilter { SearchTerm = "souza" }, CancellationToken.None);

        page.TotalItems.Should().Be(2);
        page.Items.Select(c => c.Name).Should().Equal("Bruno Lima", "Ana Souza");
    }

    [Fact]
    public async Task The_list_rejects_a_page_size_above_the_maximum()
    {
        var act = () => new ListCustomersUseCase(_customers).ExecuteAsync(
            new ListCustomersFilter { PageSize = ListCustomersFilter.MaximumPageSize + 1 }, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task Deactivating_and_reactivating_are_idempotent_and_audited_once_each()
    {
        var customer = await SaveCustomerAsync("Ana", "ana@example.com", Now);
        var useCase = new ChangeCustomerStatusUseCase(_customers, _auditLog);

        (await useCase.DeactivateAsync(customer.Id, CancellationToken.None)).Active.Should().BeFalse();
        (await useCase.DeactivateAsync(customer.Id, CancellationToken.None)).Active.Should().BeFalse();
        (await useCase.ReactivateAsync(customer.Id, CancellationToken.None)).Active.Should().BeTrue();

        _auditLog.Actions.Should().Equal("CustomerDeactivated", "CustomerReactivated");
    }

    [Fact]
    public async Task Deactivating_an_unknown_customer_is_not_found()
    {
        var act = () => new ChangeCustomerStatusUseCase(_customers, _auditLog).DeactivateAsync(Guid.NewGuid(), CancellationToken.None);

        (await act.Should().ThrowAsync<NotFoundException>()).Which.Code.Should().Be("customer_not_found");
    }

    [Fact]
    public async Task Customers_are_read_by_ids_and_counted_by_creation_period()
    {
        var old = await SaveCustomerAsync("Old", "old@example.com", Now.AddDays(-40));
        var recent = await SaveCustomerAsync("New", "new@example.com", Now.AddDays(-3));

        var found = await new GetCustomersByIdsUseCase(_customers).ExecuteAsync([old.Id, recent.Id, Guid.NewGuid()], CancellationToken.None);
        var newOnes = await new CountNewCustomersUseCase(_customers).ExecuteAsync(Now.AddDays(-30), Now, CancellationToken.None);

        found.Select(c => c.Id).Should().BeEquivalentTo([old.Id, recent.Id]);
        newOnes.Should().Be(1);
    }

    [Fact]
    public async Task Counting_new_customers_needs_a_period_that_starts_before_it_ends()
    {
        var act = () => new CountNewCustomersUseCase(_customers).ExecuteAsync(Now, Now, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
