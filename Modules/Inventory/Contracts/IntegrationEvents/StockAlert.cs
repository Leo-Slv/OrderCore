using OrderCore.Api.Shared.Application.Messaging;

namespace OrderCore.Api.Modules.Inventory.Contracts.IntegrationEvents;

/// <summary>
/// A product has just become low on stock (<c>LowStock</c>: some units
/// available, no more than the reorder level) or run out
/// (<c>OutOfStock</c>). Published only on the change of state.
/// Contract <c>inventory.stock-alert</c>, version 1.
/// </summary>
public sealed record StockAlert : IntegrationEvent
{
    public const string Name = "inventory.stock-alert";

    public required Guid ProductId { get; init; }

    /// <summary><c>LowStock</c> or <c>OutOfStock</c>.</summary>
    public required string Level { get; init; }

    public required int QuantityAvailable { get; init; }

    public required int ReorderLevel { get; init; }
}
