namespace OrderCore.Api.Modules.Customers.Infrastructure.Persistence.Models;

public sealed class CustomerPaymentMethodPersistenceModel
{
    public Guid Id { get; set; }

    public Guid CustomerId { get; set; }

    public string Provider { get; set; } = string.Empty;

    public string ProviderCustomerReference { get; set; } = string.Empty;

    public string Brand { get; set; } = string.Empty;

    public string Last4Digits { get; set; } = string.Empty;

    public int ExpiryMonth { get; set; }

    public int ExpiryYear { get; set; }

    public bool IsDefault { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public CustomerPersistenceModel Customer { get; set; } = null!;
}
