namespace OrderCore.Api.Modules.Inventory.Application.Contracts;

/// <summary>
/// Not in 04-inventory.md: every use case that mutates state touches both
/// <c>StockItem</c> (via <see cref="IStockItemRepository"/>) and
/// <c>InventoryReservation</c> (via <see cref="IInventoryReservationRepository"/>)
/// in one operation. Every other module's repository has its own
/// `SaveChangesAsync`, which only ever works because those use cases touch
/// one aggregate root per call — that stops being true here, and two
/// separate `SaveChangesAsync` calls would be two separate SQL
/// transactions, not one atomic unit. See claude.md's Transactions
/// section and Docs/specs/inventory/stock-and-reservations.md.
/// </summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
