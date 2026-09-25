using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderCore.Api.Modules.Orders.Application.DTOs;
using OrderCore.Api.Modules.Orders.Application.UseCases;
using OrderCore.Api.Modules.Orders.Presentation.Presenters;
using OrderCore.Api.Modules.Orders.Presentation.Requests;
using OrderCore.Api.Modules.Orders.Presentation.Responses;
using OrderCore.Api.Shared.Presentation.Authentication;

namespace OrderCore.Api.Modules.Orders.Presentation.Controllers;

/// <summary>
/// What an admin does to an existing order: move it through fulfilment
/// (Confirmed → Processing → Shipped → Delivered), cancel it, keep notes.
/// Each fulfilment step answers with the updated order.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("orders/{id:guid}")]
public sealed class OrderFulfilmentController : ControllerBase
{
    private readonly FulfilOrderUseCase _fulfilOrder;
    private readonly CancelOrderUseCase _cancelOrder;
    private readonly SetOrderInternalNotesUseCase _setInternalNotes;

    public OrderFulfilmentController(
        FulfilOrderUseCase fulfilOrder, CancelOrderUseCase cancelOrder, SetOrderInternalNotesUseCase setInternalNotes)
    {
        _fulfilOrder = fulfilOrder;
        _cancelOrder = cancelOrder;
        _setInternalNotes = setInternalNotes;
    }

    [HttpPost("start-processing")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponse>> StartProcessingAsync(Guid id, CancellationToken cancellationToken) =>
        Ok(OrderPresenter.ToResponse(await _fulfilOrder.StartProcessingAsync(id, cancellationToken)));

    /// <summary>
    /// Captures the payment, then marks the order shipped. If the provider
    /// refuses the capture: <c>409 payment_capture_failed</c>, and the order
    /// stays <c>Processing</c>.
    /// </summary>
    [HttpPost("ship")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderResponse>> ShipAsync(Guid id, CancellationToken cancellationToken) =>
        Ok(OrderPresenter.ToResponse(await _fulfilOrder.ShipAsync(id, cancellationToken)));

    [HttpPost("deliver")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponse>> DeliverAsync(Guid id, CancellationToken cancellationToken) =>
        Ok(OrderPresenter.ToResponse(await _fulfilOrder.DeliverAsync(id, cancellationToken)));

    /// <summary>
    /// Cancels an order that hasn't shipped, settling its payment (an
    /// authorization is voided, a capture refunded) and putting its stock
    /// back. <c>409 payment_in_progress</c> while the provider hasn't
    /// answered yet; safe to repeat after any failure.
    /// </summary>
    [HttpPost("cancel")]
    [ProducesResponseType(typeof(CancelOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CancelOrderResponse>> CancelAsync(
        Guid id, [FromBody] CancelOrderRequest request, CancellationToken cancellationToken)
    {
        var settlement = await _cancelOrder.ExecuteAsync(new CancelOrderCommand(id, request.Reason), cancellationToken);

        return Ok(new CancelOrderResponse { PaymentSettlement = settlement.ToString() });
    }

    [HttpPut("internal-notes")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetInternalNotesAsync(
        Guid id, [FromBody] SetOrderInternalNotesRequest request, CancellationToken cancellationToken)
    {
        await _setInternalNotes.ExecuteAsync(id, request.Notes, cancellationToken);

        return NoContent();
    }
}
