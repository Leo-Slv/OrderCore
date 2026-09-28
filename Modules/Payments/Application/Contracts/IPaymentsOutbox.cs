using OrderCore.Api.Shared.Application.Messaging;

namespace OrderCore.Api.Modules.Payments.Application.Contracts;

/// <summary>
/// The Payments outbox: an event enqueued here is written by the next
/// save of the payment, in the same transaction, and published afterwards
/// by the Messaging relay. One interface per publishing module, so each
/// use case gets its own module's outbox (several modules publish).
/// </summary>
public interface IPaymentsOutbox : IOutbox
{
}
