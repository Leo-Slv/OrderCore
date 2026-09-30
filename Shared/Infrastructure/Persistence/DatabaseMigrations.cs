using Microsoft.EntityFrameworkCore;

namespace OrderCore.Api.Shared.Infrastructure.Persistence;

/// <summary>A module's <c>DbContext</c> whose migrations the <c>migrate</c> command applies, and when.</summary>
public sealed record DatabaseMigrationTarget(Type DbContextType, int Order);

public static class DatabaseMigrationRegistrationExtensions
{
    /// <summary>
    /// Registers <typeparamref name="TDbContext"/> with the <c>migrate</c>
    /// command (<see cref="DatabaseMigrator"/>); each module registers its own
    /// context, the way it registers its outbox. <paramref name="order"/>
    /// fixes the sequence, so every deploy applies the contexts the same way.
    /// </summary>
    public static IServiceCollection AddDatabaseMigrations<TDbContext>(this IServiceCollection services, int order)
        where TDbContext : DbContext
    {
        services.AddSingleton(new DatabaseMigrationTarget(typeof(TDbContext), order));
        return services;
    }
}

/// <summary>
/// Applies the pending migrations of every registered module database, in
/// order — what <c>dotnet OrderCore.Api.dll migrate</c> runs as an explicit
/// deployment step (Docs/operations/deployment.md). Starting the API never
/// migrates. The first context that fails stops the run, so a deploy never
/// goes on against a half-migrated schema; running it again applies only
/// what is still pending.
/// </summary>
public sealed class DatabaseMigrator
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IReadOnlyList<DatabaseMigrationTarget> _targets;
    private readonly ILogger<DatabaseMigrator> _logger;

    public DatabaseMigrator(
        IServiceScopeFactory scopeFactory, IEnumerable<DatabaseMigrationTarget> targets, ILogger<DatabaseMigrator> logger)
    {
        _scopeFactory = scopeFactory;
        _targets = targets.OrderBy(t => t.Order).ToList();
        _logger = logger;
    }

    /// <returns><c>0</c> when every database is up to date, <c>1</c> when one failed.</returns>
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        foreach (var target in _targets)
        {
            var name = target.DbContextType.Name;
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var dbContext = (DbContext)scope.ServiceProvider.GetRequiredService(target.DbContextType);
                var pending = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
                if (pending.Count == 0)
                {
                    _logger.LogInformation("{DbContext} is up to date.", name);
                    continue;
                }

                _logger.LogInformation("{DbContext}: applying {Count} migration(s): {Migrations}.", name, pending.Count, string.Join(", ", pending));
                await dbContext.Database.MigrateAsync(cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Migrating {DbContext} failed; the remaining databases were not migrated.", name);
                return 1;
            }
        }

        _logger.LogInformation("Every database is up to date.");
        return 0;
    }
}
