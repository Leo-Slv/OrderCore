using OrderCore.Api.Modules.Customers.Application.Contracts;
using OrderCore.Api.Modules.Customers.Application.DTOs;

namespace OrderCore.Api.Modules.Customers.Application.UseCases;

/// <summary>
/// Several customers in one query, for the pages of other modules' lists
/// (e.g. the backoffice order list shows each order's customer). Customers
/// that don't exist are simply absent.
/// </summary>
public sealed class GetCustomersByIdsUseCase
{
    private readonly ICustomerRepository _customers;

    public GetCustomersByIdsUseCase(ICustomerRepository customers)
    {
        _customers = customers;
    }

    public async Task<IReadOnlyList<CustomerOutput>> ExecuteAsync(IReadOnlyCollection<Guid> customerIds, CancellationToken cancellationToken)
    {
        var distinctIds = customerIds.Distinct().ToList();
        if (distinctIds.Count == 0)
        {
            return [];
        }

        var customers = await _customers.ListByIdsAsync(distinctIds, cancellationToken);
        return customers.Select(CustomerOutput.From).ToList();
    }
}
