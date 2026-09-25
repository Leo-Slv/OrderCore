namespace OrderCore.Api.Modules.Inventory.Infrastructure.Persistence.Models;

public sealed class StockMovementPersistenceModel
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public string MovementType { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public string? ReferenceType { get; set; }

    public Guid? ReferenceId { get; set; }

    public string? Reason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
