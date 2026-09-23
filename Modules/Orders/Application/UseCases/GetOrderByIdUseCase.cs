using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Modules.Orders.Domain.Entities;

namespace OrderCore.Api.Modules.Orders.Application.UseCases;

public sealed class GetOrderByIdUseCase
{
    private readonly IOrderRepository _orderRepository;

    public GetOrderByIdUseCase(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public Task<Order?> ExecuteAsync(Guid orderId, CancellationToken cancellationToken) =>
        _orderRepository.GetByIdAsync(orderId, cancellationToken);
}
