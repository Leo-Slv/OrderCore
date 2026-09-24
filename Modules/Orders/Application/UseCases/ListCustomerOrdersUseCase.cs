using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Modules.Orders.Application.DTOs;
using OrderCore.Api.Shared.Application.DTOs;

namespace OrderCore.Api.Modules.Orders.Application.UseCases;

/// <summary>
/// A customer's order history, newest first, paged the same way
/// <c>ListAuditLogsUseCase</c> pages.
/// </summary>
public sealed class ListCustomerOrdersUseCase
{
    private readonly IOrderRepository _orderRepository;

    public ListCustomerOrdersUseCase(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<PagedResult<OrderSummaryOutput>> ExecuteAsync(ListCustomerOrdersInput input, CancellationToken cancellationToken)
    {
        if (input.Page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(input), "Page must be greater than or equal to 1.");
        }

        if (input.PageSize is < 1 or > ListCustomerOrdersInput.MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(input),
                $"PageSize must be between 1 and {ListCustomerOrdersInput.MaximumPageSize}.");
        }

        var (orders, totalCount) = await _orderRepository.ListByCustomerIdAsync(
            input.CustomerId, input.Page, input.PageSize, cancellationToken);

        return new PagedResult<OrderSummaryOutput>
        {
            Items = orders.Select(OrderSummaryOutput.From).ToList(),
            Page = input.Page,
            PageSize = input.PageSize,
            TotalItems = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)input.PageSize),
        };
    }
}
