using OrderCore.Api.Modules.Orders.Domain.Events;
using OrderCore.Api.Modules.Orders.Infrastructure.Persistence;
using OrderCore.Api.Modules.Orders.Infrastructure.Persistence.Models;
using OrderCore.Api.Shared.Application.Abstractions;

namespace OrderCore.Api.Modules.Orders.Infrastructure.EventHandlers;

/// <summary>
/// Populates <c>order_status_history</c> from Order's own domain events —
/// the second real <see cref="IDomainEventHandler{TEvent}"/> in the
/// codebase, after Inventory's <c>StockMovementRecorder</c>. Each
/// `HandleAsync` overload calls its own <see cref="OrdersDbContext.SaveChangesAsync"/>:
/// dispatch happens from <c>EfOrderRepository.SaveChangesAsync</c> *after*
/// its own save already succeeded, so this row needs its own, separate
/// commit — same trade-off <c>StockMovementRecorder</c> makes.
///
/// <see cref="OrderCancelled"/>/<see cref="OrderPaymentFailed"/> can each
/// be reached from more than one prior status (`Cancel` accepts anything
/// short of Shipped/Delivered/Cancelled), so `FromStatus` is only filled
/// in where the state machine makes it unambiguous (`OrderPaymentRequested`
/// only ever transitions from `Created`, `OrderConfirmed`/
/// `OrderPaymentFailed` only from `PendingPayment`, and the fulfilment
/// steps Confirmed → Processing → Shipped → Delivered one after another) and
/// left null otherwise, rather than guessed or looked up separately.
/// </summary>
public sealed class OrderStatusHistoryProjector :
    IDomainEventHandler<OrderCreated>,
    IDomainEventHandler<OrderPaymentRequested>,
    IDomainEventHandler<OrderConfirmed>,
    IDomainEventHandler<OrderCancelled>,
    IDomainEventHandler<OrderPaymentFailed>,
    IDomainEventHandler<OrderProcessingStarted>,
    IDomainEventHandler<OrderShipped>,
    IDomainEventHandler<OrderDelivered>
{
    private readonly OrdersDbContext _dbContext;

    public OrderStatusHistoryProjector(OrdersDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task HandleAsync(OrderCreated domainEvent, CancellationToken cancellationToken) =>
        RecordAsync(domainEvent.OrderId, fromStatus: null, toStatus: "Created", reason: null, domainEvent.OccurredAt, cancellationToken);

    public Task HandleAsync(OrderPaymentRequested domainEvent, CancellationToken cancellationToken) =>
        RecordAsync(
            domainEvent.OrderId, fromStatus: "Created", toStatus: "PendingPayment", reason: null, domainEvent.OccurredAt, cancellationToken);

    public Task HandleAsync(OrderConfirmed domainEvent, CancellationToken cancellationToken) =>
        RecordAsync(
            domainEvent.OrderId, fromStatus: "PendingPayment", toStatus: "Confirmed", reason: null, domainEvent.OccurredAt, cancellationToken);

    public Task HandleAsync(OrderCancelled domainEvent, CancellationToken cancellationToken) =>
        RecordAsync(
            domainEvent.OrderId, fromStatus: null, toStatus: "Cancelled", domainEvent.Reason, domainEvent.OccurredAt, cancellationToken);

    public Task HandleAsync(OrderPaymentFailed domainEvent, CancellationToken cancellationToken) =>
        RecordAsync(
            domainEvent.OrderId, fromStatus: "PendingPayment", toStatus: "PaymentFailed", domainEvent.Reason, domainEvent.OccurredAt,
            cancellationToken);

    public Task HandleAsync(OrderProcessingStarted domainEvent, CancellationToken cancellationToken) =>
        RecordAsync(
            domainEvent.OrderId, fromStatus: "Confirmed", toStatus: "Processing", reason: null, domainEvent.OccurredAt, cancellationToken);

    public Task HandleAsync(OrderShipped domainEvent, CancellationToken cancellationToken) =>
        RecordAsync(
            domainEvent.OrderId, fromStatus: "Processing", toStatus: "Shipped", reason: null, domainEvent.OccurredAt, cancellationToken);

    public Task HandleAsync(OrderDelivered domainEvent, CancellationToken cancellationToken) =>
        RecordAsync(
            domainEvent.OrderId, fromStatus: "Shipped", toStatus: "Delivered", reason: null, domainEvent.OccurredAt, cancellationToken);

    private async Task RecordAsync(
        Guid orderId, string? fromStatus, string toStatus, string? reason, DateTimeOffset changedAt, CancellationToken cancellationToken)
    {
        var entry = new OrderStatusHistoryPersistenceModel
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            Reason = reason,
            ChangedAt = changedAt,
        };

        await _dbContext.StatusHistory.AddAsync(entry, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
