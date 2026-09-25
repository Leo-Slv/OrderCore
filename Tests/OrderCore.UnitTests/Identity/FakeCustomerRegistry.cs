using OrderCore.Api.Modules.Identity.Application.Contracts;

namespace OrderCore.UnitTests.Identity;

internal sealed class FakeCustomerRegistry : ICustomerRegistry
{
    public List<(Guid Id, string Name, string Email)> Registered { get; } = new();

    /// <summary>Customers an admin deactivated; every other id counts as active.</summary>
    public HashSet<Guid> Inactive { get; } = new();

    /// <summary>Makes registration fail, as Customers would on a duplicate e-mail.</summary>
    public Exception? FailWith { get; set; }

    public Task<Guid> RegisterAsync(string name, string email, string? phone, CancellationToken cancellationToken)
    {
        if (FailWith is { } failure)
        {
            throw failure;
        }

        var id = Guid.NewGuid();
        Registered.Add((id, name, email));
        return Task.FromResult(id);
    }

    public Task<bool> IsActiveAsync(Guid customerId, CancellationToken cancellationToken) =>
        Task.FromResult(!Inactive.Contains(customerId));
}
