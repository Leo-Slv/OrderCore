using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Modules.Orders.Application.DTOs;
using OrderCore.Api.Shared.Application.Exceptions;

namespace OrderCore.Api.Modules.Orders.Application.UseCases;

/// <summary>
/// The backoffice order detail. The status history and the audit timeline
/// have their own endpoints (<c>orders/{id}/status-history</c>,
/// <c>audit-logs?entityName=Order&amp;entityId=…</c>).
/// </summary>
public sealed class GetAdminOrderDetailsUseCase
{
    private readonly IOrderRepository _orderRepository;
    private readonly ICustomerDirectory _customerDirectory;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IInventoryService _inventoryService;

    public GetAdminOrderDetailsUseCase(
        IOrderRepository orderRepository,
        ICustomerDirectory customerDirectory,
        IPaymentGateway paymentGateway,
        IInventoryService inventoryService)
    {
        _orderRepository = orderRepository;
        _customerDirectory = customerDirectory;
        _paymentGateway = paymentGateway;
        _inventoryService = inventoryService;
    }

    public async Task<AdminOrderDetailsOutput> ExecuteAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken)
            ?? throw new NotFoundException("order_not_found", $"Order '{orderId}' was not found.");

        var customers = await _customerDirectory.GetCustomersAsync([order.CustomerId], cancellationToken);
        var payment = await _paymentGateway.GetPaymentDetailsAsync(orderId, cancellationToken);
        var reservations = await _inventoryService.GetReservationsAsync(orderId, cancellationToken);

        return new AdminOrderDetailsOutput(order, customers.GetValueOrDefault(order.CustomerId), payment, reservations);
    }
}
