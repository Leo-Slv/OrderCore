namespace OrderCore.Api.Modules.Inventory.Application.DTOs;

/// <summary>How many stock items are low or out of stock right now (dashboard).</summary>
public sealed record StockSummaryOutput(int LowStockCount, int OutOfStockCount);
