using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Modules.Orders.Application.DTOs;
using OrderCore.Api.Modules.Orders.Domain.Entities;
using OrderCore.Api.Shared.Application.DTOs;

namespace OrderCore.Api.Modules.Orders.Application.UseCases;

/// <summary>
/// The backoffice order list: every customer's orders, filterable, newest
/// first. Each page's customers and payments are fetched in one call per
/// module, not one per order.
/// </summary>
public sealed class ListOrdersUseCase
{
    private readonly IOrderRepository _orderRepository;
    private readonly ICustomerDirectory _customerDirectory;
    private readonly IPaymentGateway _paymentGateway;

    public ListOrdersUseCase(IOrderRepository orderRepository, ICustomerDirectory customerDirectory, IPaymentGateway paymentGateway)
    {
        _orderRepository = orderRepository;
        _customerDirectory = customerDirectory;
        _paymentGateway = paymentGateway;
    }

    public async Task<PagedResult<AdminOrderSummaryOutput>> ExecuteAsync(ListOrdersFilter filter, CancellationToken cancellationToken)
    {
        if (filter.Page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(filter), "Page must be greater than or equal to 1.");
        }

        if (filter.PageSize is < 1 or > ListOrdersFilter.MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(filter),
                $"PageSize must be between 1 and {ListOrdersFilter.MaximumPageSize}.");
        }

        if (filter.CreatedFrom is { } from && filter.CreatedTo is { } to && from >= to)
        {
            throw new ArgumentException("CreatedFrom must be earlier than CreatedTo.", nameof(filter));
        }

        var (orders, totalCount) = await _orderRepository.ListAsync(filter, cancellationToken);

        return new PagedResult<AdminOrderSummaryOutput>
        {
            Items = await SummarizeAsync(orders, cancellationToken),
            Page = filter.Page,
            PageSize = filter.PageSize,
            TotalItems = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)filter.PageSize),
        };
    }

    private async Task<IReadOnlyList<AdminOrderSummaryOutput>> SummarizeAsync(
        IReadOnlyList<Order> orders, CancellationToken cancellationToken)
    {
        if (orders.Count == 0)
        {
            return [];
        }

        var customers = await _customerDirectory.GetCustomersAsync(orders.Select(o => o.CustomerId).Distinct().ToList(), cancellationToken);
        var payments = await _paymentGateway.GetPaymentSummariesAsync(orders.Select(o => o.Id).ToList(), cancellationToken);

        return orders
            .Select(o => new AdminOrderSummaryOutput(
                OrderSummaryOutput.From(o),
                customers.GetValueOrDefault(o.CustomerId),
                payments.GetValueOrDefault(o.Id)?.Status))
            .ToList();
    }
}
