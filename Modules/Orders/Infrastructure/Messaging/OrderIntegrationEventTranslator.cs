using OrderCore.Api.Modules.Orders.Domain.Entities;
using OrderCore.Api.Modules.Orders.Domain.Enums;
using OrderCore.Api.Shared.Application.Messaging;
using OrderCore.Api.Shared.Domain;
using IntegrationEvents = OrderCore.Api.Modules.Orders.Contracts.IntegrationEvents;
using DomainEvents = OrderCore.Api.Modules.Orders.Domain.Events;

namespace OrderCore.Api.Modules.Orders.Infrastructure.Messaging;

/// <summary>
/// Turns the <see cref="Order"/> aggregate's domain events into the Orders
/// contracts other modules and the browser see. Each carries the status the
/// transition moved the order to (not the order's status at save time, in
/// case one save holds several transitions) and the order's totals. The
/// integration event keeps the domain event's id, so the two can be matched.
/// </summary>
public static class OrderIntegrationEventTranslator
{
    public static IntegrationEvent? Translate(Order order, IDomainEvent domainEvent) => domainEvent switch
    {
        DomainEvents.OrderCreated e => new IntegrationEvents.OrderCreated
        {
            EventId = e.EventId,
            Version = 1,
            OccurredAt = e.OccurredAt,
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            CustomerId = order.CustomerId,
            Status = nameof(OrderStatus.Created),
            TotalAmount = order.TotalAmount,
            Currency = order.Currency,
        },
        DomainEvents.OrderPaymentRequested e => new IntegrationEvents.OrderPaymentRequested
        {
            EventId = e.EventId,
            Version = 1,
            OccurredAt = e.OccurredAt,
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            CustomerId = order.CustomerId,
            Status = nameof(OrderStatus.PendingPayment),
            TotalAmount = order.TotalAmount,
            Currency = order.Currency,
        },
        DomainEvents.OrderConfirmed e => new IntegrationEvents.OrderConfirmed
        {
            EventId = e.EventId,
            Version = 1,
            OccurredAt = e.OccurredAt,
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            CustomerId = order.CustomerId,
            Status = nameof(OrderStatus.Confirmed),
            TotalAmount = order.TotalAmount,
            Currency = order.Currency,
        },
        DomainEvents.OrderProcessingStarted e => new IntegrationEvents.OrderProcessingStarted
        {
            EventId = e.EventId,
            Version = 1,
            OccurredAt = e.OccurredAt,
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            CustomerId = order.CustomerId,
            Status = nameof(OrderStatus.Processing),
            TotalAmount = order.TotalAmount,
            Currency = order.Currency,
        },
        DomainEvents.OrderShipped e => new IntegrationEvents.OrderShipped
        {
            EventId = e.EventId,
            Version = 1,
            OccurredAt = e.OccurredAt,
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            CustomerId = order.CustomerId,
            Status = nameof(OrderStatus.Shipped),
            TotalAmount = order.TotalAmount,
            Currency = order.Currency,
        },
        DomainEvents.OrderDelivered e => new IntegrationEvents.OrderDelivered
        {
            EventId = e.EventId,
            Version = 1,
            OccurredAt = e.OccurredAt,
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            CustomerId = order.CustomerId,
            Status = nameof(OrderStatus.Delivered),
            TotalAmount = order.TotalAmount,
            Currency = order.Currency,
        },
        DomainEvents.OrderPaymentFailed e => new IntegrationEvents.OrderPaymentFailed
        {
            EventId = e.EventId,
            Version = 1,
            OccurredAt = e.OccurredAt,
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            CustomerId = order.CustomerId,
            Status = nameof(OrderStatus.PaymentFailed),
            TotalAmount = order.TotalAmount,
            Currency = order.Currency,
            Reason = e.Reason,
        },
        DomainEvents.OrderCancelled e => new IntegrationEvents.OrderCancelled
        {
            EventId = e.EventId,
            Version = 1,
            OccurredAt = e.OccurredAt,
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            CustomerId = order.CustomerId,
            Status = nameof(OrderStatus.Cancelled),
            TotalAmount = order.TotalAmount,
            Currency = order.Currency,
            Reason = e.Reason,
        },
        _ => null,
    };
}
