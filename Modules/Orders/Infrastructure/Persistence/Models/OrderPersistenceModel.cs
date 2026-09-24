namespace OrderCore.Api.Modules.Orders.Infrastructure.Persistence.Models;

/// <summary>
/// Expanded beyond 05-orders.md's abbreviated shape with every
/// <see cref="Domain.Entities.Order"/> field (OrderNumber, addresses,
/// notes, ship/deliver timestamps, UpdatedAt), same reasoning as
/// <c>CustomerAddressPersistenceModel</c>. Shipping/billing addresses are
/// flattened with a prefix rather than an owned type, matching how
/// EF Core maps <c>Address</c> for Customers today.
/// </summary>
public sealed class OrderPersistenceModel
{
    public Guid Id { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public Guid CustomerId { get; set; }

    public string Status { get; set; } = string.Empty;

    public decimal DiscountAmount { get; set; }

    public decimal ShippingAmount { get; set; }

    public decimal TaxAmount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public string? ShippingStreet { get; set; }

    public string? ShippingNumber { get; set; }

    public string? ShippingComplement { get; set; }

    public string? ShippingNeighborhood { get; set; }

    public string? ShippingCity { get; set; }

    public string? ShippingState { get; set; }

    public string? ShippingPostalCode { get; set; }

    public string? ShippingCountry { get; set; }

    public string? BillingStreet { get; set; }

    public string? BillingNumber { get; set; }

    public string? BillingComplement { get; set; }

    public string? BillingNeighborhood { get; set; }

    public string? BillingCity { get; set; }

    public string? BillingState { get; set; }

    public string? BillingPostalCode { get; set; }

    public string? BillingCountry { get; set; }

    public string? CustomerNotes { get; set; }

    public string? InternalNotes { get; set; }

    public string? CheckoutIdempotencyKey { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? ConfirmedAt { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }

    public DateTimeOffset? ShippedAt { get; set; }

    public DateTimeOffset? DeliveredAt { get; set; }

    public int Version { get; set; }

    public ICollection<OrderItemPersistenceModel> Items { get; set; } = new List<OrderItemPersistenceModel>();
}
