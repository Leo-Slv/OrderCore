using Microsoft.Extensions.DependencyInjection;
using OrderCore.Api.Modules.Payments.Contracts.IntegrationEvents;
using OrderCore.Api.Modules.Payments.Infrastructure.Messaging;
using OrderCore.Api.Modules.Payments.Infrastructure.Persistence;
using OrderCore.Api.Shared.Infrastructure.Messaging;

namespace OrderCore.IntegrationTests.Payments;

/// <summary>The Payments outbox wired by hand, for tests that build use cases without the host.</summary>
public static class PaymentsTestOutbox
{
    private static readonly IntegrationEventRegistry Registry = CreateRegistry();

    public static PaymentsOutbox For(PaymentsDbContext dbContext) =>
        new(new OutboxWriter<PaymentsDbContext>(dbContext, Registry, new MessageContext()));

    private static IntegrationEventRegistry CreateRegistry()
    {
        var services = new ServiceCollection()
            .AddIntegrationEvent<PaymentRequested>(PaymentRequested.Name, 1)
            .AddIntegrationEvent<PaymentAuthorized>(PaymentAuthorized.Name, 1)
            .AddIntegrationEvent<PaymentFailed>(PaymentFailed.Name, 1)
            .AddIntegrationEvent<PaymentCaptured>(PaymentCaptured.Name, 1)
            .AddIntegrationEvent<PaymentVoided>(PaymentVoided.Name, 1)
            .AddIntegrationEvent<PaymentRefunded>(PaymentRefunded.Name, 1);
        return services.IntegrationEventRegistry();
    }
}
