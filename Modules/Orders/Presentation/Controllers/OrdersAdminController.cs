using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderCore.Api.Modules.Orders.Application.DTOs;
using OrderCore.Api.Modules.Orders.Application.UseCases;
using OrderCore.Api.Modules.Orders.Presentation.Presenters;
using OrderCore.Api.Modules.Orders.Presentation.Responses;
using OrderCore.Api.Shared.Presentation.Authentication;
using OrderCore.Api.Shared.Presentation.Responses;

namespace OrderCore.Api.Modules.Orders.Presentation.Controllers;

/// <summary>
/// Backoffice reads that need a richer shape than the customer's, under
/// <c>admin/</c> so each route has one response shape whoever calls it.
/// The dashboard lives here because it is order-centric (see
/// <see cref="GetDashboardUseCase"/>).
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("admin")]
public sealed class OrdersAdminController : ControllerBase
{
    private readonly ListOrdersUseCase _listOrders;
    private readonly GetAdminOrderDetailsUseCase _getAdminOrderDetails;
    private readonly GetDashboardUseCase _getDashboard;

    public OrdersAdminController(
        ListOrdersUseCase listOrders, GetAdminOrderDetailsUseCase getAdminOrderDetails, GetDashboardUseCase getDashboard)
    {
        _listOrders = listOrders;
        _getAdminOrderDetails = getAdminOrderDetails;
        _getDashboard = getDashboard;
    }

    /// <summary>Every customer's orders, newest first, with the customer and payment status.</summary>
    [HttpGet("orders")]
    [ProducesResponseType(typeof(PagedResponse<AdminOrderSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<AdminOrderSummaryResponse>>> ListOrdersAsync(
        [FromQuery] ListOrdersFilter filter, CancellationToken cancellationToken) =>
        Ok(AdminOrderPresenter.ToResponse(await _listOrders.ExecuteAsync(filter, cancellationToken)));

    /// <summary>
    /// The order with its internal notes, customer, full payment and stock
    /// reservations. Its history is <c>GET orders/{id}/status-history</c>; its
    /// audit timeline <c>GET audit-logs?entityName=Order&amp;entityId={id}</c>.
    /// </summary>
    [HttpGet("orders/{id:guid}")]
    [ProducesResponseType(typeof(AdminOrderDetailsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminOrderDetailsResponse>> GetOrderAsync(Guid id, CancellationToken cancellationToken) =>
        Ok(AdminOrderPresenter.ToResponse(await _getAdminOrderDetails.ExecuteAsync(id, cancellationToken)));

    /// <summary>Summary figures for <c>[from, to)</c>; the last 30 days by default.</summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(DashboardResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DashboardResponse>> GetDashboardAsync(
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, CancellationToken cancellationToken) =>
        Ok(AdminOrderPresenter.ToResponse(await _getDashboard.ExecuteAsync(from, to, cancellationToken)));
}
