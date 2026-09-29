using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Modules.Orders.Application.DTOs;
using OrderCore.Api.Modules.Orders.Contracts.IntegrationEvents;
using OrderCore.Api.Shared.Application.Messaging;
using OrderCore.Api.Shared.Application.Observability;

namespace OrderCore.Api.Modules.Orders.Infrastructure.IntegrationEventHandlers;

/// <summary>
/// Turns every change of an order's status into a real-time update for the
/// screens following it (Docs/specs/tracking/realtime-order-tracking.md),
/// consumed from the queue <c>orders.realtime</c>. Pushes are driven by the
/// events, never sent from a use case: whatever changes an order — the
/// storefront, the backoffice, a payment outcome — reaches the screens the
/// same way, once the change was saved. It runs inside the message's consumer
/// span, so the push is part of the order's trace. Idempotent like every
/// consumer (the inbox); should a redelivery push twice anyway, the update is
/// the same, and screens ignore one older than what they show.
/// </summary>
public sealed class OrderUpdatesBroadcaster :
    IIntegrationEventHandler<OrderCreated>,
    IIntegrationEventHandler<OrderPaymentRequested>,
    IIntegrationEventHandler<OrderConfirmed>,
    IIntegrationEventHandler<OrderProcessingStarted>,
    IIntegrationEventHandler<OrderShipped>,
    IIntegrationEventHandler<OrderDelivered>,
    IIntegrationEventHandler<OrderPaymentFailed>,
    IIntegrationEventHandler<OrderCancelled>
{
    private readonly IOrderUpdatesNotifier _notifier;

    public OrderUpdatesBroadcaster(IOrderUpdatesNotifier notifier)
    {
        _notifier = notifier;
    }

    public Task HandleAsync(OrderCreated integrationEvent, CancellationToken cancellationToken) =>
        PushAsync(integrationEvent, shipment: null, cancellationToken);

    public Task HandleAsync(OrderPaymentRequested integrationEvent, CancellationToken cancellationToken) =>
        PushAsync(integrationEvent, shipment: null, cancellationToken);

    public Task HandleAsync(OrderConfirmed integrationEvent, CancellationToken cancellationToken) =>
        PushAsync(integrationEvent, shipment: null, cancellationToken);

    public Task HandleAsync(OrderProcessingStarted integrationEvent, CancellationToken cancellationToken) =>
        PushAsync(integrationEvent, shipment: null, cancellationToken);

    public Task HandleAsync(OrderShipped integrationEvent, CancellationToken cancellationToken) =>
        PushAsync(integrationEvent, ShipmentOf(integrationEvent), cancellationToken);

    public Task HandleAsync(OrderDelivered integrationEvent, CancellationToken cancellationToken) =>
        PushAsync(integrationEvent, shipment: null, cancellationToken);

    public Task HandleAsync(OrderPaymentFailed integrationEvent, CancellationToken cancellationToken) =>
        PushAsync(integrationEvent, shipment: null, cancellationToken);

    public Task HandleAsync(OrderCancelled integrationEvent, CancellationToken cancellationToken) =>
        PushAsync(integrationEvent, shipment: null, cancellationToken);

    /// <summary>The update pushed for an order event.</summary>
    public static OrderUpdate ToUpdate(OrderIntegrationEvent integrationEvent, OrderUpdateShipment? shipment) => new(
        integrationEvent.OrderId,
        integrationEvent.OrderNumber,
        integrationEvent.Status,
        integrationEvent.OccurredAt,
        integrationEvent.CustomerId,
        integrationEvent.TotalAmount,
        integrationEvent.Currency,
        shipment);

    public static OrderUpdateShipment? ShipmentOf(OrderShipped shipped) =>
        shipped.Carrier is null && shipped.TrackingCode is null && shipped.TrackingUrl is null
            ? null
            : new OrderUpdateShipment(shipped.Carrier, shipped.TrackingCode, shipped.TrackingUrl);

    private Task PushAsync(OrderIntegrationEvent integrationEvent, OrderUpdateShipment? shipment, CancellationToken cancellationToken)
    {
        Observed.Order(integrationEvent.OrderId);
        Observed.Customer(integrationEvent.CustomerId);
        return _notifier.NotifyAsync(ToUpdate(integrationEvent, shipment), cancellationToken);
    }
}
