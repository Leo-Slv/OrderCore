using OrderCore.Api.Modules.Payments.Application.Contracts;
using OrderCore.Api.Modules.Payments.Application.Contracts.IntegrationEvents;

namespace OrderCore.UnitTests.Payments;

internal sealed class FakeOutboxWriter : IOutboxWriter
{
    public List<IntegrationEvent> Enqueued { get; } = [];

    public void Enqueue(IntegrationEvent integrationEvent) => Enqueued.Add(integrationEvent);
}
