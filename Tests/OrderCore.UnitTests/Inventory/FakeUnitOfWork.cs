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

    private bool _reportDuplicate;

    public int SaveChangesCallCount { get; private set; }

    public static FakeUnitOfWork ThatConflictsThenSucceeds(int conflictCount) => new() { _conflictsRemaining = conflictCount };

    /// <summary>Simulates another request having created the same stock item first.</summary>
    public static FakeUnitOfWork ThatReportsADuplicateStockItem() => new() { _reportDuplicate = true };

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveChangesCallCount++;

        if (_reportDuplicate)
        {
            throw new DuplicateStockItemException("Simulated duplicate stock item.", new Exception());
        }

        if (_conflictsRemaining > 0)
        {
            _conflictsRemaining--;
            throw new StockConcurrencyConflictException("Simulated concurrency conflict.", new Exception());
        }

        return Task.CompletedTask;
    }
}
