using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderCore.Api.Modules.Payments.Application.DTOs;
using OrderCore.Api.Modules.Payments.Application.UseCases;
using OrderCore.Api.Modules.Payments.Presentation.Presenters;
using OrderCore.Api.Modules.Payments.Presentation.Requests;
using OrderCore.Api.Modules.Payments.Presentation.Responses;
using OrderCore.Api.Shared.Presentation.Authentication;

namespace OrderCore.Api.Modules.Payments.Presentation.Controllers;

/// <summary>
/// Thin endpoint delegating to the Payments use cases (section 39) — same
/// [ApiController]/ControllerBase shape as every other controller in the
/// project.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("payments")]
public sealed class PaymentsController : ControllerBase
{
    private readonly CreatePaymentUseCase _createPaymentUseCase;
    private readonly GetPaymentByOrderIdUseCase _getPaymentByOrderIdUseCase;
    private readonly RequestRefundUseCase _requestRefundUseCase;

    public PaymentsController(
        CreatePaymentUseCase createPaymentUseCase,
        GetPaymentByOrderIdUseCase getPaymentByOrderIdUseCase,
        RequestRefundUseCase requestRefundUseCase)
    {
        _createPaymentUseCase = createPaymentUseCase;
        _getPaymentByOrderIdUseCase = getPaymentByOrderIdUseCase;
        _requestRefundUseCase = requestRefundUseCase;
    }

    [HttpPost]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaymentResponse>> CreateAsync([FromBody] CreatePaymentRequest request, CancellationToken cancellationToken)
    {
        var command = new CreatePaymentCommand(
            request.OrderId, request.Amount, request.Currency, request.Method!.Value, request.IdempotencyKey);
        await _createPaymentUseCase.ExecuteAsync(command, cancellationToken);

        // Re-read rather than building the response from CreatePaymentResult
        // directly: that result only has PaymentId/Status, not OrderId/Amount
        // — same reasoning as OrdersController.CreateOrderAsync.
        var payment = await _getPaymentByOrderIdUseCase.ExecuteAsync(request.OrderId, cancellationToken);
        var response = PaymentPresenter.ToResponse(payment!);

        return CreatedAtAction(nameof(GetByOrderIdAsync), new { orderId = response.OrderId }, response);
    }

    [HttpGet("orders/{orderId:guid}")]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaymentResponse>> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var payment = await _getPaymentByOrderIdUseCase.ExecuteAsync(orderId, cancellationToken);

        return payment is null ? NotFound() : Ok(PaymentPresenter.ToResponse(payment));
    }

    [HttpPost("{id:guid}/refunds")]
    [ProducesResponseType(typeof(RefundResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RefundResponse>> RequestRefundAsync(
        Guid id, [FromBody] RequestRefundRequest request, CancellationToken cancellationToken)
    {
        var refund = await _requestRefundUseCase.ExecuteAsync(new RequestRefundCommand(id, request.Amount, request.Reason), cancellationToken);

        return StatusCode(StatusCodes.Status201Created, PaymentPresenter.ToResponse(refund));
    }
}
