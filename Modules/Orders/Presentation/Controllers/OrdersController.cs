using Microsoft.AspNetCore.Mvc;
using OrderCore.Api.Modules.Orders.Application.DTOs;
using OrderCore.Api.Modules.Orders.Application.UseCases;
using OrderCore.Api.Modules.Orders.Presentation.Presenters;
using OrderCore.Api.Modules.Orders.Presentation.Requests;
using OrderCore.Api.Modules.Orders.Presentation.Responses;
using OrderCore.Api.Shared.Domain.ValueObjects;

namespace OrderCore.Api.Modules.Orders.Presentation.Controllers;

/// <summary>
/// Thin endpoint delegating to the Orders use cases (section 39) — same
/// [ApiController]/ControllerBase shape as CustomersController/CatalogController/
/// InventoryController.
/// </summary>
[ApiController]
[Route("orders")]
public sealed class OrdersController : ControllerBase
{
    private readonly CreateOrderHandler _createOrderHandler;
    private readonly SetOrderAddressesUseCase _setOrderAddressesUseCase;
    private readonly RequestOrderPaymentUseCase _requestOrderPaymentUseCase;
    private readonly GetOrderByIdUseCase _getOrderByIdUseCase;
    private readonly ListCustomerOrdersUseCase _listCustomerOrdersUseCase;
    private readonly CancelOrderUseCase _cancelOrderUseCase;

    public OrdersController(
        CreateOrderHandler createOrderHandler,
        SetOrderAddressesUseCase setOrderAddressesUseCase,
        RequestOrderPaymentUseCase requestOrderPaymentUseCase,
        GetOrderByIdUseCase getOrderByIdUseCase,
        ListCustomerOrdersUseCase listCustomerOrdersUseCase,
        CancelOrderUseCase cancelOrderUseCase)
    {
        _createOrderHandler = createOrderHandler;
        _setOrderAddressesUseCase = setOrderAddressesUseCase;
        _requestOrderPaymentUseCase = requestOrderPaymentUseCase;
        _getOrderByIdUseCase = getOrderByIdUseCase;
        _listCustomerOrdersUseCase = listCustomerOrdersUseCase;
        _cancelOrderUseCase = cancelOrderUseCase;
    }

    [HttpPost]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponse>> CreateOrderAsync([FromBody] CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateOrderCommand(
            request.CustomerId, request.Currency, request.Items.Select(i => new CreateOrderItem(i.ProductId, i.Quantity)).ToList());

        var result = await _createOrderHandler.HandleAsync(command, cancellationToken);

        // Re-read rather than using OrderPresenter.ToResponse(CreateOrderResult)
        // directly: CreateOrderResult doesn't carry OrderNumber/Currency/
        // items, so this is the only way to return a complete OrderResponse.
        var order = await _getOrderByIdUseCase.ExecuteAsync(result.OrderId, cancellationToken);
        var response = OrderPresenter.ToResponse(order!);

        return CreatedAtAction(nameof(GetByIdAsync), new { id = response.Id }, response);
    }

    [HttpPut("{id:guid}/addresses")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetAddressesAsync(Guid id, [FromBody] SetOrderAddressesRequest request, CancellationToken cancellationToken)
    {
        var command = new SetOrderAddressesCommand(
            id,
            Address.Create(
                request.ShippingStreet, request.ShippingNumber, request.ShippingComplement, request.ShippingNeighborhood,
                request.ShippingCity, request.ShippingState, request.ShippingPostalCode, request.ShippingCountry),
            Address.Create(
                request.BillingStreet, request.BillingNumber, request.BillingComplement, request.BillingNeighborhood,
                request.BillingCity, request.BillingState, request.BillingPostalCode, request.BillingCountry));

        await _setOrderAddressesUseCase.ExecuteAsync(command, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Returns bare <see cref="IActionResult"/>, matching 05-orders.md's
    /// signature exactly: confirming or failing the order happens later,
    /// asynchronously (see <see cref="RequestOrderPaymentUseCase"/>'s
    /// remarks), so a response body claiming a final <c>OrderResponse</c>
    /// here would be misleading regardless of what
    /// <c>CreateOrderResult</c> (which doesn't carry enough to build one
    /// anyway) could offer.
    /// </summary>
    [HttpPost("{id:guid}/request-payment")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RequestPaymentAsync(Guid id, CancellationToken cancellationToken)
    {
        await _requestOrderPaymentUseCase.ExecuteAsync(id, cancellationToken);

        return Accepted();
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var order = await _getOrderByIdUseCase.ExecuteAsync(id, cancellationToken);

        return order is null ? NotFound() : Ok(OrderPresenter.ToResponse(order));
    }

    [HttpGet("customers/{customerId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<OrderResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OrderResponse>>> ListByCustomerAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var orders = await _listCustomerOrdersUseCase.ExecuteAsync(customerId, cancellationToken);

        return Ok(orders.Select(OrderPresenter.ToResponse).ToList());
    }

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CancelAsync(Guid id, [FromBody] CancelOrderRequest request, CancellationToken cancellationToken)
    {
        await _cancelOrderUseCase.ExecuteAsync(new CancelOrderCommand(id, request.Reason), cancellationToken);

        return NoContent();
    }
}
