using OrderCore.Api.Modules.Inventory.Application.Contracts;

namespace OrderCore.UnitTests.Inventory;

/// <summary>
/// Lets tests simulate the optimistic-concurrency conflict
/// <c>ReserveStockUseCase</c>'s retry loop is meant to recover from,
/// without needing a real database.
/// </summary>
internal sealed class FakeUnitOfWork : IUnitOfWork
{
    private int _conflictsRemaining;

    public int SaveChangesCallCount { get; private set; }

    public static FakeUnitOfWork ThatConflictsThenSucceeds(int conflictCount) => new() { _conflictsRemaining = conflictCount };

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveChangesCallCount++;

        if (_conflictsRemaining > 0)
        {
            _conflictsRemaining--;
            throw new StockConcurrencyConflictException("Simulated concurrency conflict.", new Exception());
        }

        return Task.CompletedTask;
    }
}
