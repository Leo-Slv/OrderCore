using OrderCore.Api.Modules.Orders.Domain.Entities;

namespace OrderCore.Api.Modules.Orders.Application.DTOs;

/// <summary>
/// The backoffice order detail: everything the customer sees (the order,
/// its items and addresses) plus the internal notes (on <see cref="Order"/>),
/// the customer, the payment in full and the stock reservations.
/// </summary>
public sealed record AdminOrderDetailsOutput(
    Order Order,
    OrderCustomerSnapshot? Customer,
    OrderPaymentDetails? Payment,
    IReadOnlyList<OrderReservationSummary> Reservations);
