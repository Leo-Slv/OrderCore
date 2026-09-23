using Microsoft.AspNetCore.Mvc;
using OrderCore.Api.Modules.Inventory.Application.UseCases;
using OrderCore.Api.Modules.Inventory.Presentation.Presenters;
using OrderCore.Api.Modules.Inventory.Presentation.Requests;
using OrderCore.Api.Modules.Inventory.Presentation.Responses;

namespace OrderCore.Api.Modules.Inventory.Presentation.Controllers;

/// <summary>
/// Thin endpoint delegating to the Inventory use cases (section 39) — same
/// [ApiController]/ControllerBase shape as CustomersController/CatalogController.
/// Reserve/Release/Consume/ExpireReservationUseCase have no route here:
/// 04-inventory.md's InventoryController never wires them to an endpoint
/// (they stay reachable only from other Application-layer code — Orders,
/// once its own InventoryServiceAdapter exists), same as
/// ChangeProductPriceUseCase in Catalog.
/// </summary>
[ApiController]
[Route("api/inventory")]
public sealed class InventoryController : ControllerBase
{
    private readonly GetStockByProductIdUseCase _getStockByProductIdUseCase;
    private readonly AdjustStockUseCase _adjustStockUseCase;

    public InventoryController(GetStockByProductIdUseCase getStockByProductIdUseCase, AdjustStockUseCase adjustStockUseCase)
    {
        _getStockByProductIdUseCase = getStockByProductIdUseCase;
        _adjustStockUseCase = adjustStockUseCase;
    }

    [HttpGet("stock-items/{productId:guid}")]
    [ProducesResponseType(typeof(StockItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockItemResponse>> GetStockByProductIdAsync(Guid productId, CancellationToken cancellationToken)
    {
        var output = await _getStockByProductIdUseCase.ExecuteAsync(productId, cancellationToken);

        return Ok(StockItemPresenter.ToResponse(output));
    }

    [HttpPost("stock-items/{productId:guid}/adjust")]
    [ProducesResponseType(typeof(StockItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StockItemResponse>> AdjustStockAsync(
        Guid productId,
        [FromBody] AdjustStockRequest request,
        CancellationToken cancellationToken)
    {
        var output = await _adjustStockUseCase.ExecuteAsync(productId, request.Quantity, request.Reason, cancellationToken);

        return Ok(StockItemPresenter.ToResponse(output));
    }
}
