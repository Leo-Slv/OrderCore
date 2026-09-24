using OrderCore.Api.Modules.Orders.Application.UseCases;
using OrderCore.Api.Modules.Payments.Application.Contracts.IntegrationEvents;
using OrderCore.Api.Shared.Application.Abstractions;

namespace OrderCore.Api.Modules.Orders.Infrastructure.IntegrationEventHandlers;

/// <summary>
/// Reacts to <see cref="PaymentFailed"/> — see
/// <see cref="PaymentAuthorizedIntegrationEventHandler"/>'s remarks.
/// </summary>
public sealed class PaymentFailedIntegrationEventHandler : IDomainEventHandler<PaymentFailed>
{
    private readonly MarkOrderPaymentFailedUseCase _markPaymentFailed;

    public PaymentFailedIntegrationEventHandler(MarkOrderPaymentFailedUseCase markPaymentFailed)
    {
        _markPaymentFailed = markPaymentFailed;
    }

    public Task HandleAsync(PaymentFailed domainEvent, CancellationToken cancellationToken) =>
        _markPaymentFailed.ExecuteAsync(domainEvent.OrderId, domainEvent.Reason, cancellationToken);
}
