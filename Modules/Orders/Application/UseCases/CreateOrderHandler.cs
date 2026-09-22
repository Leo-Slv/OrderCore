using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Modules.Orders.Application.DTOs;
using OrderCore.Api.Modules.Orders.Domain.Entities;

namespace OrderCore.Api.Modules.Orders.Application.UseCases;

/// <summary>
/// Orchestrates the "create order" use case. Note what this class does
/// NOT do: it does not reach into the Inventory or Payments modules'
/// internal structures (section 7) — inventory reservation and payment
/// initiation are separate steps/use cases, invoked through their own
/// application contracts, so the order-creation flow stays a thin,
/// testable orchestration over the Order aggregate.
/// </summary>
public sealed class CreateOrderHandler
{
    private readonly IOrderRepository _orderRepository;
    private readonly IProductCatalog _productCatalog;
    private readonly TimeProvider _timeProvider;

    public CreateOrderHandler(IOrderRepository orderRepository, IProductCatalog productCatalog, TimeProvider timeProvider)
    {
        _orderRepository = orderRepository;
        _productCatalog = productCatalog;
        _timeProvider = timeProvider;
    }

    public async Task<CreateOrderResult> HandleAsync(CreateOrderCommand command, CancellationToken cancellationToken)
    {
        if (command.Items.Count == 0)
        {
            throw new InvalidOperationException("An order must contain at least one item.");
        }

        var now = _timeProvider.GetUtcNow();
        var order = Order.Create(command.CustomerId, command.Currency, now);

        foreach (var item in command.Items)
        {
            var product = await _productCatalog.GetAsync(item.ProductId, cancellationToken)
                ?? throw new InvalidOperationException($"Product '{item.ProductId}' was not found.");

            order.AddItem(product.Id, product.Name, product.CurrentPrice, item.Quantity);
        }

        await _orderRepository.AddAsync(order, cancellationToken);
        await _orderRepository.SaveChangesAsync(cancellationToken);

        return new CreateOrderResult(order.Id, order.TotalAmount, order.Status.ToString());
    }
}
