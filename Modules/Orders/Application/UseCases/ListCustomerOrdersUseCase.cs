using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Modules.Orders.Domain.Entities;

namespace OrderCore.Api.Modules.Orders.Application.UseCases;

public sealed class ListCustomerOrdersUseCase
{
    private readonly IOrderRepository _orderRepository;

    public ListCustomerOrdersUseCase(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public Task<IReadOnlyList<Order>> ExecuteAsync(Guid customerId, CancellationToken cancellationToken) =>
        _orderRepository.ListByCustomerIdAsync(customerId, cancellationToken);
}
