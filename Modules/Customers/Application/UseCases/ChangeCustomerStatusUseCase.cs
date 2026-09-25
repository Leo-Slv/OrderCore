using OrderCore.Api.Modules.AuditLogs.Application.Constants;
using OrderCore.Api.Modules.AuditLogs.Application.Services;
using OrderCore.Api.Modules.Customers.Application.Contracts;
using OrderCore.Api.Modules.Customers.Application.DTOs;
using OrderCore.Api.Modules.Customers.Domain.Entities;
using OrderCore.Api.Shared.Application.Exceptions;

namespace OrderCore.Api.Modules.Customers.Application.UseCases;

/// <summary>
/// An admin deactivates or reactivates a customer. A deactivated customer
/// can't check out (<c>customer_inactive</c>) and can't sign in or refresh a
/// session (Identity asks through <c>ICustomerRegistry.IsActiveAsync</c>);
/// an access token already issued lives out its few minutes. Their orders
/// and data stay as they are. Both are idempotent: asking for the state the
/// customer is already in changes and records nothing.
/// </summary>
public sealed class ChangeCustomerStatusUseCase
{
    private readonly ICustomerRepository _customers;
    private readonly IAuditLogService _auditLog;

    public ChangeCustomerStatusUseCase(ICustomerRepository customers, IAuditLogService auditLog)
    {
        _customers = customers;
        _auditLog = auditLog;
    }

    public Task<CustomerOutput> DeactivateAsync(Guid customerId, CancellationToken cancellationToken) =>
        SetActiveAsync(customerId, active: false, cancellationToken);

    public Task<CustomerOutput> ReactivateAsync(Guid customerId, CancellationToken cancellationToken) =>
        SetActiveAsync(customerId, active: true, cancellationToken);

    private async Task<CustomerOutput> SetActiveAsync(Guid customerId, bool active, CancellationToken cancellationToken)
    {
        var customer = await _customers.GetByIdAsync(customerId, cancellationToken)
            ?? throw new NotFoundException("customer_not_found", $"Customer '{customerId}' was not found.");

        if (customer.Active == active)
        {
            return CustomerOutput.From(customer);
        }

        Apply(customer, active);
        await _customers.SaveChangesAsync(cancellationToken);

        await _auditLog.RecordAsync(
            active ? AuditLogActionNames.CustomerReactivated : AuditLogActionNames.CustomerDeactivated,
            "Customer",
            customerId,
            metadata: null,
            userId: null,
            cancellationToken);

        return CustomerOutput.From(customer);
    }

    private static void Apply(Customer customer, bool active)
    {
        if (active)
        {
            customer.Activate();
        }
        else
        {
            customer.Deactivate();
        }
    }
}
