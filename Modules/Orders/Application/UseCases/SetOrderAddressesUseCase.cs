using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Modules.Orders.Application.DTOs;
using OrderCore.Api.Shared.Application.Exceptions;

namespace OrderCore.Api.Modules.Orders.Application.UseCases;

public sealed class SetOrderAddressesUseCase
{
    private readonly IOrderRepository _orderRepository;

    public SetOrderAddressesUseCase(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task ExecuteAsync(SetOrderAddressesCommand command, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(command.OrderId, cancellationToken)
            ?? throw new NotFoundException("order_not_found", $"Order '{command.OrderId}' was not found.");

        order.SetAddresses(command.ShippingAddress, command.BillingAddress);
        await _orderRepository.SaveChangesAsync(cancellationToken);
    }
}
