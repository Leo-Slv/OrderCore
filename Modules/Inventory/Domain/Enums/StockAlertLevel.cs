namespace OrderCore.Api.Modules.Inventory.Domain.Enums;

/// <summary>The stock states worth warning about (see <c>StockItem.IsLowStock</c>).</summary>
public enum StockAlertLevel
{
    /// <summary>Some units are available, but no more than the reorder level.</summary>
    LowStock,

    /// <summary>No unit is available.</summary>
    OutOfStock,
}
