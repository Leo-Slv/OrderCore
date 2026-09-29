using OrderCore.Api.Modules.Orders.Application.DTOs;

namespace OrderCore.Api.Modules.Orders.Application.Contracts;

/// <summary>
/// Pushes an order update to whoever may see it and is connected right now:
/// the order's customer and the admins. Implemented in Presentation over
/// SignalR (the same "Presentation implements an Application abstraction"
/// shape as <c>ICurrentUser</c>). Nobody connected is not an error — the
/// update is simply not delivered, and a screen that reconnects reloads.
/// </summary>
public interface IOrderUpdatesNotifier
{
    Task NotifyAsync(OrderUpdate update, CancellationToken cancellationToken);
}
