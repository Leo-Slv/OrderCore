using System.Globalization;
using System.Text.Json;
using OrderCore.Api.Modules.Inventory.Contracts.IntegrationEvents;
using OrderCore.Api.Modules.Orders.Contracts.IntegrationEvents;
using OrderCore.Api.Modules.Orders.Infrastructure.Persistence;
using OrderCore.Api.Modules.Orders.Infrastructure.Persistence.Models;
using OrderCore.Api.Modules.Payments.Contracts.IntegrationEvents;
using OrderCore.Api.Shared.Application.Messaging;
using OrderCore.Api.Shared.Infrastructure.Messaging;

namespace OrderCore.Api.Modules.Orders.Infrastructure.IntegrationEventHandlers;

/// <summary>
/// Files every integration event about an order — its own lifecycle,
/// Payments' and Inventory's reservation movements — into
/// <c>order_timeline</c>, consumed from the queue <c>orders.timeline</c>
/// (Docs/specs/events/async-messaging.md, decision 4: the timeline is a
/// projection of the events). One row per event, keyed by the event id, so
/// a redelivery never files it twice. Each row keeps a few facts worth
/// showing; the admin reads them through <c>GET admin/orders/{id}/timeline</c>.
/// </summary>
public sealed class OrderTimelineProjector :
    IIntegrationEventHandler<OrderCreated>,
    IIntegrationEventHandler<OrderPaymentRequested>,
    IIntegrationEventHandler<OrderConfirmed>,
    IIntegrationEventHandler<OrderProcessingStarted>,
    IIntegrationEventHandler<OrderShipped>,
    IIntegrationEventHandler<OrderDelivered>,
    IIntegrationEventHandler<OrderPaymentFailed>,
    IIntegrationEventHandler<OrderCancelled>,
    IIntegrationEventHandler<PaymentRequested>,
    IIntegrationEventHandler<PaymentAuthorized>,
    IIntegrationEventHandler<PaymentFailed>,
    IIntegrationEventHandler<PaymentCaptured>,
    IIntegrationEventHandler<PaymentVoided>,
    IIntegrationEventHandler<PaymentRefunded>,
    IIntegrationEventHandler<StockReserved>,
    IIntegrationEventHandler<StockReleased>,
    IIntegrationEventHandler<StockConsumed>,
    IIntegrationEventHandler<StockReturned>
{
    private readonly OrdersDbContext _dbContext;
    private readonly IntegrationEventRegistry _registry;

    public OrderTimelineProjector(OrdersDbContext dbContext, IntegrationEventRegistry registry)
    {
        _dbContext = dbContext;
        _registry = registry;
    }

    public Task HandleAsync(OrderCreated integrationEvent, CancellationToken cancellationToken) =>
        FileAsync(integrationEvent.OrderId, integrationEvent, Money(integrationEvent.TotalAmount, integrationEvent.Currency), cancellationToken);

    public Task HandleAsync(OrderPaymentRequested integrationEvent, CancellationToken cancellationToken) =>
        FileAsync(integrationEvent.OrderId, integrationEvent, [], cancellationToken);

    public Task HandleAsync(OrderConfirmed integrationEvent, CancellationToken cancellationToken) =>
        FileAsync(integrationEvent.OrderId, integrationEvent, [], cancellationToken);

    public Task HandleAsync(OrderProcessingStarted integrationEvent, CancellationToken cancellationToken) =>
        FileAsync(integrationEvent.OrderId, integrationEvent, [], cancellationToken);

    public Task HandleAsync(OrderShipped integrationEvent, CancellationToken cancellationToken) =>
        FileAsync(integrationEvent.OrderId, integrationEvent, [], cancellationToken);

    public Task HandleAsync(OrderDelivered integrationEvent, CancellationToken cancellationToken) =>
        FileAsync(integrationEvent.OrderId, integrationEvent, [], cancellationToken);

    public Task HandleAsync(OrderPaymentFailed integrationEvent, CancellationToken cancellationToken) =>
        FileAsync(integrationEvent.OrderId, integrationEvent, Reason(integrationEvent.Reason), cancellationToken);

    public Task HandleAsync(OrderCancelled integrationEvent, CancellationToken cancellationToken) =>
        FileAsync(integrationEvent.OrderId, integrationEvent, Reason(integrationEvent.Reason), cancellationToken);

    public Task HandleAsync(PaymentRequested integrationEvent, CancellationToken cancellationToken) =>
        FileAsync(integrationEvent.OrderId, integrationEvent, Money(integrationEvent.Amount, integrationEvent.Currency), cancellationToken);

    public Task HandleAsync(PaymentAuthorized integrationEvent, CancellationToken cancellationToken) =>
        FileAsync(integrationEvent.OrderId, integrationEvent, Money(integrationEvent.Amount, integrationEvent.Currency), cancellationToken);

    public Task HandleAsync(PaymentFailed integrationEvent, CancellationToken cancellationToken) =>
        FileAsync(integrationEvent.OrderId, integrationEvent, Reason(integrationEvent.Reason), cancellationToken);

    public Task HandleAsync(PaymentCaptured integrationEvent, CancellationToken cancellationToken) =>
        FileAsync(integrationEvent.OrderId, integrationEvent, Money(integrationEvent.Amount, integrationEvent.Currency), cancellationToken);

    public Task HandleAsync(PaymentVoided integrationEvent, CancellationToken cancellationToken) =>
        FileAsync(integrationEvent.OrderId, integrationEvent, [], cancellationToken);

    public Task HandleAsync(PaymentRefunded integrationEvent, CancellationToken cancellationToken) =>
        FileAsync(integrationEvent.OrderId, integrationEvent, [], cancellationToken);

    public Task HandleAsync(StockReserved integrationEvent, CancellationToken cancellationToken) =>
        FileAsync(integrationEvent.OrderId, integrationEvent, Stock(integrationEvent), cancellationToken);

    public Task HandleAsync(StockReleased integrationEvent, CancellationToken cancellationToken) =>
        FileAsync(integrationEvent.OrderId, integrationEvent, Stock(integrationEvent), cancellationToken);

    public Task HandleAsync(StockConsumed integrationEvent, CancellationToken cancellationToken) =>
        FileAsync(integrationEvent.OrderId, integrationEvent, Stock(integrationEvent), cancellationToken);

    public Task HandleAsync(StockReturned integrationEvent, CancellationToken cancellationToken) =>
        FileAsync(integrationEvent.OrderId, integrationEvent, Stock(integrationEvent), cancellationToken);

    /// <summary>
    /// Adds the row to the Orders context; the consumer host saves it together
    /// with the inbox row, so the event is filed exactly when it is marked handled.
    /// </summary>
    private Task FileAsync(
        Guid orderId, IntegrationEvent integrationEvent, Dictionary<string, string?> details, CancellationToken cancellationToken)
    {
        var contract = _registry.ContractOf(integrationEvent.GetType());

        _dbContext.Timeline.Add(new OrderTimelineEntryPersistenceModel
        {
            EventId = integrationEvent.EventId,
            OrderId = orderId,
            Type = contract.Name,
            Source = contract.Name.Split('.')[0],
            OccurredAt = integrationEvent.OccurredAt,
            DetailsJson = JsonSerializer.Serialize(details),
        });

        return Task.CompletedTask;
    }

    private static Dictionary<string, string?> Money(decimal amount, string currency) => new()
    {
        ["amount"] = amount.ToString(CultureInfo.InvariantCulture),
        ["currency"] = currency,
    };

    private static Dictionary<string, string?> Reason(string reason) => new() { ["reason"] = reason };

    private static Dictionary<string, string?> Stock(StockReservationIntegrationEvent integrationEvent) => new()
    {
        ["productId"] = integrationEvent.ProductId.ToString(),
        ["quantity"] = integrationEvent.Quantity.ToString(CultureInfo.InvariantCulture),
    };
}
