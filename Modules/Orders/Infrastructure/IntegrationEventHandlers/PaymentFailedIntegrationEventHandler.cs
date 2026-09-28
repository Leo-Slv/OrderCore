using OrderCore.Api.Modules.Orders.Application.UseCases;
using OrderCore.Api.Modules.Payments.Contracts.IntegrationEvents;
using OrderCore.Api.Shared.Application.Messaging;

namespace OrderCore.Api.Modules.Orders.Infrastructure.IntegrationEventHandlers;

/// <summary>
/// Marks the order as payment failed when Payments announces
/// <see cref="PaymentFailed"/> — see <see cref="PaymentAuthorizedIntegrationEventHandler"/>.
/// </summary>
public sealed class PaymentFailedIntegrationEventHandler : IIntegrationEventHandler<PaymentFailed>
{
    private readonly MarkOrderPaymentFailedUseCase _markPaymentFailed;

    public PaymentFailedIntegrationEventHandler(MarkOrderPaymentFailedUseCase markPaymentFailed)
    {
        _markPaymentFailed = markPaymentFailed;
    }

    public Task HandleAsync(PaymentFailed integrationEvent, CancellationToken cancellationToken) =>
        _markPaymentFailed.ExecuteAsync(integrationEvent.OrderId, integrationEvent.Reason, cancellationToken);
}
