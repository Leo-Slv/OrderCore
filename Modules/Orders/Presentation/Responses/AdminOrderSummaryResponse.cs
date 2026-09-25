namespace OrderCore.Api.Modules.Orders.Presentation.Responses;

/// <summary>
/// One row of <c>GET admin/orders</c>. <see cref="PaymentStatus"/> is the
/// payment's status (<c>Authorized</c>, <c>Captured</c>, <c>Voided</c>…), or
/// null when no payment was requested. <see cref="Customer"/> is null only if
/// the customer no longer exists.
/// </summary>
public sealed class AdminOrderSummaryResponse
{
    public Guid Id { get; init; }

    public string OrderNumber { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }

    public decimal TotalAmount { get; init; }

    public string Currency { get; init; } = string.Empty;

    public int ItemCount { get; init; }

    public OrderCustomerResponse? Customer { get; init; }

    public string? PaymentStatus { get; init; }
}
