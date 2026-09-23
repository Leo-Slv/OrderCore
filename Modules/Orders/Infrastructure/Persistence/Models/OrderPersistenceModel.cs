namespace OrderCore.Api.Modules.Orders.Infrastructure.Persistence.Models;

public sealed class OrderPersistenceModel
{
    public Guid Id { get; set; }

    public Guid CustomerId { get; set; }

    public string Status { get; set; } = string.Empty;

    public string Currency { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ConfirmedAt { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }

    public int Version { get; set; }

    public ICollection<OrderItemPersistenceModel> Items { get; set; } = new List<OrderItemPersistenceModel>();
}
