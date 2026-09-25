using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderCore.Api.Modules.Inventory.Application.DTOs;
using OrderCore.Api.Modules.Inventory.Application.UseCases;
using OrderCore.Api.Modules.Inventory.Presentation.Presenters;
using OrderCore.Api.Modules.Inventory.Presentation.Requests;
using OrderCore.Api.Modules.Inventory.Presentation.Responses;
using OrderCore.Api.Shared.Presentation.Authentication;
using OrderCore.Api.Shared.Presentation.Responses;

namespace OrderCore.Api.Modules.Inventory.Presentation.Controllers;

/// <summary>
/// Thin endpoint delegating to the Inventory use cases (section 39) — same
/// [ApiController]/ControllerBase shape as CustomersController/CatalogController.
/// Reserve/Release/Consume/Expire and returning an order's stock have no
/// route here: they are steps of the order lifecycle, reachable only
/// through Orders' <c>InventoryServiceAdapter</c>. Nor does creating a
/// stock record: Catalog ensures one exists for every product.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("inventory")]
public sealed class InventoryController : ControllerBase
{
    private readonly GetStockByProductIdUseCase _getStockByProductIdUseCase;
    private readonly AdjustStockUseCase _adjustStockUseCase;
    private readonly ReceiveStockUseCase _receiveStockUseCase;
    private readonly SetReorderLevelUseCase _setReorderLevelUseCase;
    private readonly ListStockItemsUseCase _listStockItemsUseCase;
    private readonly ListStockMovementsUseCase _listStockMovementsUseCase;
    private readonly ListReservationsUseCase _listReservationsUseCase;

    public InventoryController(
        GetStockByProductIdUseCase getStockByProductIdUseCase,
        AdjustStockUseCase adjustStockUseCase,
        ReceiveStockUseCase receiveStockUseCase,
        SetReorderLevelUseCase setReorderLevelUseCase,
        ListStockItemsUseCase listStockItemsUseCase,
        ListStockMovementsUseCase listStockMovementsUseCase,
        ListReservationsUseCase listReservationsUseCase)
    {
        _getStockByProductIdUseCase = getStockByProductIdUseCase;
        _adjustStockUseCase = adjustStockUseCase;
        _receiveStockUseCase = receiveStockUseCase;
        _setReorderLevelUseCase = setReorderLevelUseCase;
        _listStockItemsUseCase = listStockItemsUseCase;
        _listStockMovementsUseCase = listStockMovementsUseCase;
        _listReservationsUseCase = listReservationsUseCase;
    }

    /// <summary>
    /// Stock figures by product id, optionally only low or out of stock.
    /// The backoffice stock screen uses <c>GET admin/catalog/products</c>,
    /// which adds product names and SKUs.
    /// </summary>
    [HttpGet("stock-items")]
    [ProducesResponseType(typeof(PagedResponse<StockItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<StockItemResponse>>> ListStockItemsAsync(
        [FromQuery] ListStockItemsFilter filter, CancellationToken cancellationToken)
    {
        var page = await _listStockItemsUseCase.ExecuteAsync(filter, cancellationToken);

        return Ok(StockItemPresenter.ToResponse(page, StockItemPresenter.ToResponse));
    }

    [HttpGet("stock-items/{productId:guid}")]
    [ProducesResponseType(typeof(StockItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockItemResponse>> GetStockByProductIdAsync(Guid productId, CancellationToken cancellationToken)
    {
        var output = await _getStockByProductIdUseCase.ExecuteAsync(productId, cancellationToken);

        return Ok(StockItemPresenter.ToResponse(output));
    }

    [HttpPost("stock-items/{productId:guid}/receive")]
    [ProducesResponseType(typeof(StockItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StockItemResponse>> ReceiveStockAsync(
        Guid productId,
        [FromBody] ReceiveStockRequest request,
        CancellationToken cancellationToken)
    {
        var output = await _receiveStockUseCase.ExecuteAsync(productId, request.Quantity, request.Reason, cancellationToken);

        return Ok(StockItemPresenter.ToResponse(output));
    }

    [HttpPost("stock-items/{productId:guid}/adjust")]
    [ProducesResponseType(typeof(StockItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StockItemResponse>> AdjustStockAsync(
        Guid productId,
        [FromBody] AdjustStockRequest request,
        CancellationToken cancellationToken)
    {
        var output = await _adjustStockUseCase.ExecuteAsync(productId, request.Quantity, request.Reason, cancellationToken);

        return Ok(StockItemPresenter.ToResponse(output));
    }

    [HttpPut("stock-items/{productId:guid}/reorder-level")]
    [ProducesResponseType(typeof(StockItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StockItemResponse>> SetReorderLevelAsync(
        Guid productId,
        [FromBody] SetReorderLevelRequest request,
        CancellationToken cancellationToken)
    {
        var output = await _setReorderLevelUseCase.ExecuteAsync(productId, request.ReorderLevel, cancellationToken);

        return Ok(StockItemPresenter.ToResponse(output));
    }

    /// <summary>The product's stock history, newest first.</summary>
    [HttpGet("stock-items/{productId:guid}/movements")]
    [ProducesResponseType(typeof(PagedResponse<StockMovementResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<StockMovementResponse>>> ListMovementsAsync(
        Guid productId,
        [FromQuery] int page = InventoryPaging.DefaultPage,
        [FromQuery] int pageSize = InventoryPaging.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var movements = await _listStockMovementsUseCase.ExecuteAsync(productId, page, pageSize, cancellationToken);

        return Ok(StockItemPresenter.ToResponse(movements, StockItemPresenter.ToResponse));
    }

    /// <summary>The product's reservations, newest first.</summary>
    [HttpGet("stock-items/{productId:guid}/reservations")]
    [ProducesResponseType(typeof(PagedResponse<ReservationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<ReservationResponse>>> ListReservationsAsync(
        Guid productId,
        [FromQuery] int page = InventoryPaging.DefaultPage,
        [FromQuery] int pageSize = InventoryPaging.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var reservations = await _listReservationsUseCase.ForProductAsync(productId, page, pageSize, cancellationToken);

        return Ok(StockItemPresenter.ToResponse(reservations, StockItemPresenter.ToResponse));
    }
}
