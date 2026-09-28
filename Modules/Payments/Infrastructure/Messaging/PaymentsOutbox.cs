using OrderCore.Api.Modules.Payments.Application.Contracts;
using OrderCore.Api.Modules.Payments.Infrastructure.Persistence;
using OrderCore.Api.Shared.Application.Messaging;
using OrderCore.Api.Shared.Infrastructure.Messaging;

namespace OrderCore.Api.Modules.Payments.Infrastructure.Messaging;

/// <summary><see cref="IPaymentsOutbox"/> over <c>payments_outbox_messages</c> in <see cref="PaymentsDbContext"/>.</summary>
public sealed class PaymentsOutbox : IPaymentsOutbox
{
    private readonly OutboxWriter<PaymentsDbContext> _writer;

    public PaymentsOutbox(OutboxWriter<PaymentsDbContext> writer)
    {
        _writer = writer;
    }

    public void Enqueue(IntegrationEvent integrationEvent) => _writer.Enqueue(integrationEvent);
}
