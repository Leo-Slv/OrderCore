using OrderCore.Api.Modules.Payments.Application.Contracts;
using OrderCore.Api.Modules.Payments.Contracts.IntegrationEvents;
using OrderCore.Api.Shared.Application.Messaging;

namespace OrderCore.UnitTests.Payments;

internal sealed class FakePaymentsOutbox : IPaymentsOutbox
{
    public List<IntegrationEvent> Enqueued { get; } = [];

    public void Enqueue(IntegrationEvent integrationEvent) => Enqueued.Add(integrationEvent);
}
