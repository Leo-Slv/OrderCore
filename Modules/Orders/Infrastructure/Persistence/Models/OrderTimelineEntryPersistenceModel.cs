namespace OrderCore.Api.Modules.Orders.Infrastructure.Persistence.Models;

/// <summary>One integration event about an order, as filed by <c>OrderTimelineProjector</c>.</summary>
public sealed class OrderTimelineEntryPersistenceModel
{
    /// <summary>The event's id: an event is filed once, however many times it is delivered.</summary>
    public Guid EventId { get; set; }

    public Guid OrderId { get; set; }

    /// <summary>
    /// Database-assigned, increasing in insert order; breaks ties between
    /// events that happened at the same instant.
    /// </summary>
    public long Sequence { get; set; }

    /// <summary>The contract name, e.g. <c>payments.payment-authorized</c>.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>The module that published it: <c>orders</c>, <c>payments</c> or <c>inventory</c>.</summary>
    public string Source { get; set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>A few facts worth showing (amount, reason, product and quantity), as a JSON object of strings.</summary>
    public string DetailsJson { get; set; } = "{}";
}
