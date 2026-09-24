using OrderCore.Api.Modules.Orders.Application.UseCases;
using OrderCore.Api.Modules.Payments.Application.Contracts.IntegrationEvents;
using OrderCore.Api.Shared.Application.Abstractions;

namespace OrderCore.Api.Modules.Orders.Infrastructure.IntegrationEventHandlers;

/// <summary>
/// Reacts to <see cref="PaymentAuthorized"/> — published by Payments'
/// <c>OutboxPublisherBackgroundService</c>, which calls
/// <see cref="IDomainEventDispatcher"/> directly (the resolved outbox
/// bridge — see Docs/specs/payments/payment-processing.md), reachable here
/// as a real <see cref="IDomainEventHandler{TEvent}"/> because
/// <see cref="IntegrationEvent"/> now implements <c>IDomainEvent</c>.
/// </summary>
public sealed class PaymentAuthorizedIntegrationEventHandler : IDomainEventHandler<PaymentAuthorized>
{
    private readonly ConfirmOrderUseCase _confirmOrder;

    public PaymentAuthorizedIntegrationEventHandler(ConfirmOrderUseCase confirmOrder)
    {
        _confirmOrder = confirmOrder;
    }

    public Task HandleAsync(PaymentAuthorized domainEvent, CancellationToken cancellationToken) =>
        _confirmOrder.ExecuteAsync(domainEvent.OrderId, cancellationToken);
}
