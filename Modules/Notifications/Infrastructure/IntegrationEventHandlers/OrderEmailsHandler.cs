using OrderCore.Api.Modules.Notifications.Application.UseCases;
using OrderCore.Api.Modules.Orders.Contracts.IntegrationEvents;
using OrderCore.Api.Shared.Application.Messaging;

namespace OrderCore.Api.Modules.Notifications.Infrastructure.IntegrationEventHandlers;

/// <summary>
/// The order e-mails (password-recovery spec, item 6), consumed from the queue
/// <c>notifications.order-emails</c>: Orders' events say what changed, this
/// turns each into an <see cref="OrderEmail"/>. The inbox lives in
/// Notifications' own context, so it commits with the queued e-mail — a
/// redelivered event queues nothing new.
/// </summary>
public sealed class OrderEmailsHandler :
    IIntegrationEventHandler<OrderConfirmed>,
    IIntegrationEventHandler<OrderShipped>,
    IIntegrationEventHandler<OrderCancelled>,
    IIntegrationEventHandler<OrderPaymentFailed>
{
    public const string Queue = "notifications.order-emails";

    private readonly QueueOrderEmailUseCase _queueOrderEmail;

    public OrderEmailsHandler(QueueOrderEmailUseCase queueOrderEmail)
    {
        _queueOrderEmail = queueOrderEmail;
    }

    public Task HandleAsync(OrderConfirmed integrationEvent, CancellationToken cancellationToken) =>
        _queueOrderEmail.ExecuteAsync(EmailOf(OrderEmailKind.Confirmed, integrationEvent), cancellationToken);

    public Task HandleAsync(OrderShipped integrationEvent, CancellationToken cancellationToken) =>
        _queueOrderEmail.ExecuteAsync(
            EmailOf(OrderEmailKind.Shipped, integrationEvent) with
            {
                Carrier = integrationEvent.Carrier,
                TrackingCode = integrationEvent.TrackingCode,
                TrackingUrl = integrationEvent.TrackingUrl,
            },
            cancellationToken);

    public Task HandleAsync(OrderCancelled integrationEvent, CancellationToken cancellationToken) =>
        _queueOrderEmail.ExecuteAsync(EmailOf(OrderEmailKind.Cancelled, integrationEvent) with { Reason = integrationEvent.Reason }, cancellationToken);

    public Task HandleAsync(OrderPaymentFailed integrationEvent, CancellationToken cancellationToken) =>
        _queueOrderEmail.ExecuteAsync(
            EmailOf(OrderEmailKind.PaymentFailed, integrationEvent) with { Reason = integrationEvent.Reason }, cancellationToken);

    private static OrderEmail EmailOf(OrderEmailKind kind, OrderIntegrationEvent e) =>
        new(kind, e.OrderId, e.OrderNumber, e.CustomerId, e.TotalAmount, e.Currency);
}
