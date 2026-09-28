using OrderCore.Api.Modules.Orders.Application.DTOs;

namespace OrderCore.Api.Modules.Orders.Application.Contracts;

/// <summary>
/// Read side of <c>order_timeline</c>, which <c>OrderTimelineProjector</c>
/// fills from the integration events of every module. Entries come back in
/// the order they happened.
/// </summary>
public interface IOrderTimelineReader
{
    Task<IReadOnlyList<OrderTimelineEntry>> ListAsync(Guid orderId, CancellationToken cancellationToken);
}
