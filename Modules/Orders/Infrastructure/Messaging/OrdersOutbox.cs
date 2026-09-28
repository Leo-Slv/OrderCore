using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Modules.Orders.Infrastructure.Persistence;
using OrderCore.Api.Shared.Application.Messaging;
using OrderCore.Api.Shared.Infrastructure.Messaging;

namespace OrderCore.Api.Modules.Orders.Infrastructure.Messaging;

/// <summary><see cref="IOrdersOutbox"/> over <c>orders_outbox_messages</c> in <see cref="OrdersDbContext"/>.</summary>
public sealed class OrdersOutbox : IOrdersOutbox
{
    private readonly OutboxWriter<OrdersDbContext> _writer;

    public OrdersOutbox(OutboxWriter<OrdersDbContext> writer)
    {
        _writer = writer;
    }

    public void Enqueue(IntegrationEvent integrationEvent) => _writer.Enqueue(integrationEvent);
}
