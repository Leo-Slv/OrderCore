using OrderCore.Api.Modules.Inventory.Application.Contracts;
using OrderCore.Api.Modules.Inventory.Infrastructure.Persistence;
using OrderCore.Api.Shared.Application.Messaging;
using OrderCore.Api.Shared.Infrastructure.Messaging;

namespace OrderCore.Api.Modules.Inventory.Infrastructure.Messaging;

/// <summary><see cref="IInventoryOutbox"/> over <c>inventory_outbox_messages</c> in <see cref="InventoryDbContext"/>.</summary>
public sealed class InventoryOutbox : IInventoryOutbox
{
    private readonly OutboxWriter<InventoryDbContext> _writer;

    public InventoryOutbox(OutboxWriter<InventoryDbContext> writer)
    {
        _writer = writer;
    }

    public void Enqueue(IntegrationEvent integrationEvent) => _writer.Enqueue(integrationEvent);
}
