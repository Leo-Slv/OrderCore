namespace OrderCore.Api.Modules.Customers.Infrastructure.Persistence.Models;

/// <summary>
/// EF Core persistence model for <see cref="Domain.Entities.Customer"/>.
/// Kept separate from the domain entity per 02-customers.md / the
/// project's Domain Entity ↔ Mapper ↔ Persistence Model ↔ EF Core shape, so
/// EF Core's mapping concerns (navigation properties, nullable reference
/// defaults) never leak into the aggregate.
/// </summary>
public sealed class CustomerPersistenceModel
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? DocumentNumber { get; set; }

    public bool Active { get; set; }

    public DateTimeOffset? EmailVerifiedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public int Version { get; set; }

    public ICollection<CustomerAddressPersistenceModel> Addresses { get; set; } =
        new List<CustomerAddressPersistenceModel>();

    public ICollection<CustomerPaymentMethodPersistenceModel> PaymentMethods { get; set; } =
        new List<CustomerPaymentMethodPersistenceModel>();
}
