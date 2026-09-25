namespace OrderCore.Api.Modules.Orders.Application.DTOs;

/// <summary>
/// One row of the backoffice order list: the order summary plus who bought
/// it and where its payment stands. <see cref="Customer"/> is null only if
/// the customer no longer exists; <see cref="PaymentStatus"/> is null when
/// no payment was requested (Payments' status name otherwise).
/// </summary>
public sealed record AdminOrderSummaryOutput(OrderSummaryOutput Order, OrderCustomerSnapshot? Customer, string? PaymentStatus);
