using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using OrderCore.Api.Modules.Orders.Application.DTOs;
using OrderCore.Api.Modules.Orders.Application.UseCases;
using OrderCore.Api.Modules.Orders.Presentation.Presenters;
using OrderCore.Api.Modules.Orders.Presentation.Requests;
using OrderCore.Api.Modules.Orders.Presentation.Responses;
using OrderCore.Api.Shared.Domain.ValueObjects;
using OrderCore.Api.Shared.Presentation.Responses;

namespace OrderCore.Api.Modules.Orders.Presentation.Controllers;

/// <summary>
/// Thin endpoint delegating to the Orders use cases (section 39) — same
/// [ApiController]/ControllerBase shape as CustomersController/CatalogController/
/// InventoryController.
///
/// The storefront uses <see cref="QuoteCartAsync"/>, <see cref="CheckoutAsync"/>
/// and the read endpoints. The step-by-step create → set addresses →
/// request payment endpoints stay for manual/admin flows (resolved
/// decision 3 in Docs/specs/storefront/storefront-api-mvp.md).
/// </summary>
[ApiController]
[Route("orders")]
public sealed class OrdersController : ControllerBase
{
    private readonly CreateOrderHandler _createOrderHandler;
    private readonly SetOrderAddressesUseCase _setOrderAddressesUseCase;
    private readonly RequestOrderPaymentUseCase _requestOrderPaymentUseCase;
    private readonly GetOrderByIdUseCase _getOrderByIdUseCase;
    private readonly GetOrderDetailsUseCase _getOrderDetailsUseCase;
    private readonly GetOrderStatusHistoryUseCase _getOrderStatusHistoryUseCase;
    private readonly ListCustomerOrdersUseCase _listCustomerOrdersUseCase;
    private readonly CancelOrderUseCase _cancelOrderUseCase;
    private readonly CheckoutUseCase _checkoutUseCase;
    private readonly QuoteCartUseCase _quoteCartUseCase;

    public OrdersController(
        CreateOrderHandler createOrderHandler,
        SetOrderAddressesUseCase setOrderAddressesUseCase,
        RequestOrderPaymentUseCase requestOrderPaymentUseCase,
        GetOrderByIdUseCase getOrderByIdUseCase,
        GetOrderDetailsUseCase getOrderDetailsUseCase,
        GetOrderStatusHistoryUseCase getOrderStatusHistoryUseCase,
        ListCustomerOrdersUseCase listCustomerOrdersUseCase,
        CancelOrderUseCase cancelOrderUseCase,
        CheckoutUseCase checkoutUseCase,
        QuoteCartUseCase quoteCartUseCase)
    {
        _createOrderHandler = createOrderHandler;
        _setOrderAddressesUseCase = setOrderAddressesUseCase;
        _requestOrderPaymentUseCase = requestOrderPaymentUseCase;
        _getOrderByIdUseCase = getOrderByIdUseCase;
        _getOrderDetailsUseCase = getOrderDetailsUseCase;
        _getOrderStatusHistoryUseCase = getOrderStatusHistoryUseCase;
        _listCustomerOrdersUseCase = listCustomerOrdersUseCase;
        _cancelOrderUseCase = cancelOrderUseCase;
        _checkoutUseCase = checkoutUseCase;
        _quoteCartUseCase = quoteCartUseCase;
    }

    /// <summary>
    /// Re-prices a client-side cart against the current catalog and stock.
    /// Read-only: nothing is reserved.
    /// </summary>
    [HttpPost("cart/quote")]
    [ProducesResponseType(typeof(CartQuoteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CartQuoteResponse>> QuoteCartAsync([FromBody] QuoteCartRequest request, CancellationToken cancellationToken)
    {
        var quote = await _quoteCartUseCase.ExecuteAsync(OrderPresenter.ToLines(request), cancellationToken);

        return Ok(OrderPresenter.ToResponse(quote));
    }

    /// <summary>
    /// Turns a cart into an order awaiting payment: validates, reserves
    /// stock and starts payment in one request. Answers 202 with the order
    /// in <c>PendingPayment</c>; poll <c>GET orders/{id}</c> for the outcome.
    /// Replaying the request with the same <c>Idempotency-Key</c> returns the
    /// same order and never creates a second one.
    /// </summary>
    [HttpPost("checkout")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderResponse>> CheckoutAsync(
        [FromBody] CheckoutRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required, MaxLength(100)] string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var orderId = await _checkoutUseCase.ExecuteAsync(OrderPresenter.ToCommand(request, idempotencyKey), cancellationToken);
        var details = await _getOrderDetailsUseCase.ExecuteAsync(orderId, cancellationToken);

        return AcceptedAtAction(nameof(GetByIdAsync), new { id = orderId }, OrderPresenter.ToResponse(details));
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

        // Re-read rather than building the response from CreateOrderResult:
        // it doesn't carry OrderNumber/Currency/items, so this is the only
        // way to return a complete OrderResponse.
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
    /// here would be misleading.
    /// </summary>
    [HttpPost("{id:guid}/request-payment")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RequestPaymentAsync(
        Guid id, [FromBody] RequestOrderPaymentRequest request, CancellationToken cancellationToken)
    {
        await _requestOrderPaymentUseCase.ExecuteAsync(id, request.PaymentMethod!.Value, cancellationToken);

        return Accepted();
    }

    /// <summary>
    /// The order with its payment status. The tracking screen polls this
    /// while the payment outcome is on its way.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var details = await _getOrderDetailsUseCase.ExecuteAsync(id, cancellationToken);

        return Ok(OrderPresenter.ToResponse(details));
    }

    /// <summary>Recorded status transitions, oldest first: the tracking timeline.</summary>
    [HttpGet("{id:guid}/status-history")]
    [ProducesResponseType(typeof(IReadOnlyList<OrderStatusHistoryEntryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<OrderStatusHistoryEntryResponse>>> GetStatusHistoryAsync(
        Guid id, CancellationToken cancellationToken)
    {
        var history = await _getOrderStatusHistoryUseCase.ExecuteAsync(id, cancellationToken);

        return Ok(OrderPresenter.ToResponse(history));
    }

    /// <summary>A customer's orders, newest first.</summary>
    [HttpGet("customers/{customerId:guid}")]
    [ProducesResponseType(typeof(PagedResponse<OrderSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<OrderSummaryResponse>>> ListByCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken,
        [FromQuery] int page = ListCustomerOrdersInput.DefaultPage,
        [FromQuery] int pageSize = ListCustomerOrdersInput.DefaultPageSize)
    {
        var orders = await _listCustomerOrdersUseCase.ExecuteAsync(
            new ListCustomerOrdersInput { CustomerId = customerId, Page = page, PageSize = pageSize }, cancellationToken);

        return Ok(OrderPresenter.ToResponse(orders));
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
