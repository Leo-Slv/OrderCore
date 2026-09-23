namespace OrderCore.Api.Modules.Inventory.Application.Contracts;

/// <summary>
/// Thrown by <see cref="IUnitOfWork.SaveChangesAsync"/> when a concurrent
/// update changed a <c>StockItem</c> between when it was loaded and when
/// this unit of work tried to save — the optimistic-concurrency signal
/// <c>ReserveStockUseCase</c> retries on (section 11 of the project
/// context). An Application-layer type on purpose, so use cases don't
/// need to reference EF Core's own <c>DbUpdateConcurrencyException</c>.
/// </summary>
public sealed class StockConcurrencyConflictException : Exception
{
    public StockConcurrencyConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
