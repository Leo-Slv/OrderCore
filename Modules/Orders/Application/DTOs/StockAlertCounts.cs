namespace OrderCore.Api.Modules.Orders.Application.DTOs;

/// <summary>How many products are low or out of stock right now (dashboard).</summary>
public sealed record StockAlertCounts(int LowStock, int OutOfStock);
