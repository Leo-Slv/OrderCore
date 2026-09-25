using OrderCore.Api.Modules.Customers.Application.Contracts;
using OrderCore.Api.Modules.Customers.Application.DTOs;
using OrderCore.Api.Shared.Application.DTOs;

namespace OrderCore.Api.Modules.Customers.Application.UseCases;

/// <summary>
/// The backoffice customer list. A customer's orders are not included: the
/// customer detail screen gets them from Orders (<c>GET orders/customers/{id}</c>),
/// since Customers doesn't depend on Orders.
/// </summary>
public sealed class ListCustomersUseCase
{
    private readonly ICustomerRepository _customers;

    public ListCustomersUseCase(ICustomerRepository customers)
    {
        _customers = customers;
    }

    public async Task<PagedResult<CustomerOutput>> ExecuteAsync(ListCustomersFilter filter, CancellationToken cancellationToken)
    {
        if (filter.Page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(filter), "Page must be greater than or equal to 1.");
        }

        if (filter.PageSize is < 1 or > ListCustomersFilter.MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(filter),
                $"PageSize must be between 1 and {ListCustomersFilter.MaximumPageSize}.");
        }

        var (customers, totalCount) = await _customers.ListAsync(filter.SearchTerm, filter.Page, filter.PageSize, cancellationToken);

        return new PagedResult<CustomerOutput>
        {
            Items = customers.Select(CustomerOutput.From).ToList(),
            Page = filter.Page,
            PageSize = filter.PageSize,
            TotalItems = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)filter.PageSize),
        };
    }
}
