using OrderCore.Api.Modules.Orders.Application.UseCases;
using OrderCore.Api.Modules.Payments.Contracts.IntegrationEvents;
using OrderCore.Api.Shared.Application.Messaging;

namespace OrderCore.Api.Modules.Orders.Infrastructure.IntegrationEventHandlers;

/// <summary>
/// Cancels the order when Payments announces
/// <see cref="PaymentAuthorizationExpired"/>, consumed from
/// <c>orders.payment-outcomes</c>; the inbox row commits with the order's save.
/// </summary>
public sealed class PaymentAuthorizationExpiredIntegrationEventHandler : IIntegrationEventHandler<PaymentAuthorizationExpired>
{
    private readonly CancelOrderOnExpiredAuthorizationUseCase _cancelOrder;

    public PaymentAuthorizationExpiredIntegrationEventHandler(CancelOrderOnExpiredAuthorizationUseCase cancelOrder)
    {
        _cancelOrder = cancelOrder;
    }

    public Task HandleAsync(PaymentAuthorizationExpired integrationEvent, CancellationToken cancellationToken) =>
        _cancelOrder.ExecuteAsync(integrationEvent.OrderId, cancellationToken);
}
