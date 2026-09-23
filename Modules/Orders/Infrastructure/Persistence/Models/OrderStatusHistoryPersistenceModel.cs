namespace OrderCore.Api.Modules.Orders.Infrastructure.Persistence.Models;

public sealed class OrderStatusHistoryPersistenceModel
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public string? FromStatus { get; set; }

    public string ToStatus { get; set; } = string.Empty;

    public string? Reason { get; set; }

    public DateTimeOffset ChangedAt { get; set; }
}
