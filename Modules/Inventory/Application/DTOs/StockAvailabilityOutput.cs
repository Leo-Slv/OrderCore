namespace OrderCore.Api.Modules.Inventory.Application.DTOs;

/// <summary>
/// What other modules may learn about a product's stock (see
/// <c>GetStockAvailabilityUseCase</c>). <see cref="QuantityAvailable"/> is
/// for internal decisions such as "is there enough for this cart line";
/// callers decide how much of it their own clients get to see.
/// </summary>
public sealed record StockAvailabilityOutput(Guid ProductId, int QuantityAvailable, bool IsLowStock);
