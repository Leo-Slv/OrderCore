namespace OrderCore.Api.Modules.Inventory.Application.DTOs;

/// <summary>
/// A stock item's state as the backoffice sees it, from its available
/// units (on hand minus reserved) and its reorder level: out of stock at
/// 0 or less, low at or below the reorder level, in stock otherwise —
/// the same rule as <c>StockItem.IsLowStock</c>.
/// </summary>
public enum StockState
{
    InStock,
    LowStock,
    OutOfStock,
}
