using OrderCore.Api.Modules.Inventory.Application.Contracts;
using OrderCore.Api.Modules.Inventory.Application.DTOs;
using OrderCore.Api.Shared.Application.DTOs;

namespace OrderCore.Api.Modules.Inventory.Application.UseCases;

/// <summary>
/// Reservations of a product (backoffice stock screen) or of an order
/// (the admin order detail, through Orders' adapter).
/// </summary>
public sealed class ListReservationsUseCase
{
    private readonly IInventoryReservationRepository _reservations;

    public ListReservationsUseCase(IInventoryReservationRepository reservations)
    {
        _reservations = reservations;
    }

    public async Task<PagedResult<ReservationOutput>> ForProductAsync(
        Guid productId, int page, int pageSize, CancellationToken cancellationToken)
    {
        InventoryPaging.Validate(page, pageSize);

        var (items, totalCount) = await _reservations.ListByProductIdAsync(productId, page, pageSize, cancellationToken);

        return InventoryPaging.ToPagedResult(items.Select(ReservationOutput.From).ToList(), page, pageSize, totalCount);
    }

    /// <summary>An order has one reservation per item, so this isn't paged.</summary>
    public async Task<IReadOnlyList<ReservationOutput>> ForOrderAsync(Guid orderId, CancellationToken cancellationToken) =>
        (await _reservations.ListByOrderIdAsync(orderId, cancellationToken))
            .OrderBy(r => r.ReservedAt)
            .Select(ReservationOutput.From)
            .ToList();
}
