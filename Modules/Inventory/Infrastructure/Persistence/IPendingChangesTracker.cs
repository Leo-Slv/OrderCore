using OrderCore.Api.Shared.Domain;

namespace OrderCore.Api.Modules.Inventory.Infrastructure.Persistence;

/// <summary>
/// Implemented by both <c>EfStockItemRepository</c> and
/// <c>EfInventoryReservationRepository</c> so <c>InventoryUnitOfWork</c>
/// can coordinate them into a single atomic save — see
/// <c>IUnitOfWork</c>'s remarks (Application layer) for why a single
/// `SaveChangesAsync` per repository doesn't work once a use case touches
/// two aggregate roots at once. `internal`: this is a wiring detail
/// between Inventory's own repositories and its own unit of work, not
/// part of the Application-facing contract.
/// </summary>
internal interface IPendingChangesTracker
{
    /// <summary>
    /// Reconciles every tracked domain instance's current state onto its
    /// persistence model, right before the shared DbContext is saved.
    /// </summary>
    void ApplyPendingChanges();

    /// <summary>
    /// Returns every tracked aggregate's domain events and clears them —
    /// called only after the save has actually succeeded.
    /// </summary>
    IReadOnlyCollection<IDomainEvent> CollectAndClearDomainEvents();

    /// <summary>
    /// Drops all tracked entries after a concurrency conflict, forcing the
    /// next read to hit the database instead of returning stale
    /// already-tracked state.
    /// </summary>
    void ForgetTrackedEntries();
}
