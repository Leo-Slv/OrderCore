namespace OrderCore.Api.Modules.Inventory.Application.DTOs;

/// <summary>
/// One line of a product's stock history. <see cref="Quantity"/> is signed
/// for adjustments; <see cref="ReferenceType"/>/<see cref="ReferenceId"/>
/// say what caused it (a reservation or the stock item itself).
/// </summary>
public sealed record StockMovementOutput(
    Guid Id,
    Guid ProductId,
    string MovementType,
    int Quantity,
    string? ReferenceType,
    Guid? ReferenceId,
    string? Reason,
    DateTimeOffset CreatedAt);
