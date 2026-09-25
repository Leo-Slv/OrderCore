using OrderCore.Api.Modules.Customers.Application.Contracts;

namespace OrderCore.Api.Modules.Customers.Application.UseCases;

/// <summary>Customers who signed up in <c>[from, to)</c>, for the dashboard.</summary>
public sealed class CountNewCustomersUseCase
{
    private readonly ICustomerRepository _customers;

    public CountNewCustomersUseCase(ICustomerRepository customers)
    {
        _customers = customers;
    }

    public Task<int> ExecuteAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        if (from >= to)
        {
            throw new ArgumentException("The period must start before it ends.", nameof(from));
        }

        return _customers.CountCreatedBetweenAsync(from, to, cancellationToken);
    }
}
