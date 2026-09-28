using OrderCore.Api.Shared.Application.Messaging;

namespace OrderCore.Api.Modules.Inventory.Application.Contracts;

/// <summary>
/// The Inventory outbox: an event enqueued here is written by the module's next
/// save, in the same transaction, and published afterwards by the
/// Messaging relay (see <c>IPaymentsOutbox</c>).
/// </summary>
public interface IInventoryOutbox : IOutbox
{
}
