using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Modules.Identity.Domain.Entities;
using OrderCore.Api.Modules.Identity.Infrastructure.Persistence;
using OrderCore.Api.Modules.Identity.Infrastructure.Persistence.Repositories;
using Testcontainers.PostgreSql;
using Xunit;

namespace OrderCore.IntegrationTests.Identity;

/// <summary>
/// Same shape as the other repository tests: real PostgreSQL, real
/// migration. Sessions are added to accounts that are already saved,
/// the case that silently failed for other modules' child entities
/// before (see the child-key rule in CLAUDE.md).
/// </summary>
public sealed class EfUserAccountRepositoryTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private IdentityDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options);

    private async Task<Guid> SaveCustomerAccountAsync(string email)
    {
        await using var dbContext = CreateDbContext();
        var repository = new EfUserAccountRepository(dbContext);
        var account = UserAccount.CreateCustomer(email, "hash", Now);
        account.LinkCustomer(Guid.NewGuid());
        await repository.AddAsync(account, CancellationToken.None);
        await repository.SaveChangesAsync(CancellationToken.None);
        return account.Id;
    }

    [Fact]
    public async Task Account_round_trips_and_is_found_by_normalized_email()
    {
        var accountId = await SaveCustomerAccountAsync("Jane@Example.com");

        await using var dbContext = CreateDbContext();
        var reloaded = await new EfUserAccountRepository(dbContext)
            .GetByNormalizedEmailAsync(UserAccount.NormalizeEmail("jane@example.COM"), CancellationToken.None);

        reloaded!.Id.Should().Be(accountId);
        reloaded.Email.Should().Be("Jane@Example.com");
        reloaded.CustomerId.Should().NotBeNull();
    }

    [Fact]
    public async Task Two_accounts_cannot_share_an_email()
    {
        await SaveCustomerAccountAsync("jane@example.com");

        var act = () => SaveCustomerAccountAsync("JANE@example.com");

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Sessions_started_and_rotated_on_a_saved_account_are_persisted_and_found_by_token_hash()
    {
        var accountId = await SaveCustomerAccountAsync("jane@example.com");

        await using (var dbContext = CreateDbContext())
        {
            var repository = new EfUserAccountRepository(dbContext);
            var account = await repository.GetByIdAsync(accountId, CancellationToken.None);
            account!.StartSession("hash-1", Now.AddDays(14), Now);
            await repository.SaveChangesAsync(CancellationToken.None);
        }

        await using (var dbContext = CreateDbContext())
        {
            var repository = new EfUserAccountRepository(dbContext);
            var account = await repository.GetBySessionTokenHashAsync("hash-1", CancellationToken.None);
            account!.RotateSession("hash-1", "hash-2", Now.AddDays(14), Now.AddMinutes(1));
            await repository.SaveChangesAsync(CancellationToken.None);
        }

        await using (var dbContext = CreateDbContext())
        {
            var account = await new EfUserAccountRepository(dbContext).GetBySessionTokenHashAsync("hash-2", CancellationToken.None);

            account!.Id.Should().Be(accountId);
            account.Sessions.Should().HaveCount(2);
            account.Sessions.Single(s => s.TokenHash == "hash-1").RevokedAt.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task Pruned_expired_sessions_are_deleted()
    {
        var accountId = await SaveCustomerAccountAsync("jane@example.com");

        await using (var dbContext = CreateDbContext())
        {
            var repository = new EfUserAccountRepository(dbContext);
            var account = await repository.GetByIdAsync(accountId, CancellationToken.None);
            account!.StartSession("short-lived", Now.AddMinutes(5), Now);
            await repository.SaveChangesAsync(CancellationToken.None);
        }

        await using (var dbContext = CreateDbContext())
        {
            var repository = new EfUserAccountRepository(dbContext);
            var account = await repository.GetByIdAsync(accountId, CancellationToken.None);
            account!.StartSession("next", Now.AddDays(14), Now.AddHours(1));
            await repository.SaveChangesAsync(CancellationToken.None);
        }

        await using (var dbContext = CreateDbContext())
        {
            (await dbContext.RefreshSessions.Select(s => s.TokenHash).ToListAsync()).Should().Equal("next");
        }
    }

    [Fact]
    public async Task Removing_an_account_deletes_it_with_its_sessions()
    {
        await using var dbContext = CreateDbContext();
        var repository = new EfUserAccountRepository(dbContext);
        var account = UserAccount.CreateCustomer("jane@example.com", "hash", Now);
        account.StartSession("hash-1", Now.AddDays(14), Now);
        await repository.AddAsync(account, CancellationToken.None);
        await repository.SaveChangesAsync(CancellationToken.None);

        repository.Remove(account);
        await repository.SaveChangesAsync(CancellationToken.None);

        (await dbContext.UserAccounts.CountAsync()).Should().Be(0);
        (await dbContext.RefreshSessions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AnyAdminAsync_is_true_only_once_an_admin_exists()
    {
        await SaveCustomerAccountAsync("jane@example.com");

        await using var dbContext = CreateDbContext();
        var repository = new EfUserAccountRepository(dbContext);
        (await repository.AnyAdminAsync(CancellationToken.None)).Should().BeFalse();

        await repository.AddAsync(UserAccount.CreateAdmin("admin@example.com", "hash", Now), CancellationToken.None);
        await repository.SaveChangesAsync(CancellationToken.None);

        (await repository.AnyAdminAsync(CancellationToken.None)).Should().BeTrue();
    }
}
