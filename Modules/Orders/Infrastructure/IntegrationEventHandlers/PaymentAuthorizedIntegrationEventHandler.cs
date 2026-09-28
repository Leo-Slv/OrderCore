using OrderCore.Api.Modules.Orders.Application.UseCases;
using OrderCore.Api.Modules.Payments.Contracts.IntegrationEvents;
using OrderCore.Api.Shared.Application.Messaging;

namespace OrderCore.Api.Modules.Orders.Infrastructure.IntegrationEventHandlers;

/// <summary>
/// Confirms the order when Payments announces <see cref="PaymentAuthorized"/>,
/// consumed from the queue <c>orders.payment-outcomes</c>. The inbox row
/// (<c>orders_processed_messages</c>) commits with the order's save, so a
/// redelivery does nothing; an order that has moved on is skipped by the
/// use case, never an error that would be retried forever.
/// </summary>
public sealed class PaymentAuthorizedIntegrationEventHandler : IIntegrationEventHandler<PaymentAuthorized>
{
    private readonly ConfirmOrderUseCase _confirmOrder;

    public PaymentAuthorizedIntegrationEventHandler(ConfirmOrderUseCase confirmOrder)
    {
        _confirmOrder = confirmOrder;
    }

    public Task HandleAsync(PaymentAuthorized integrationEvent, CancellationToken cancellationToken) =>
        _confirmOrder.ExecuteAsync(integrationEvent.OrderId, cancellationToken);
}
