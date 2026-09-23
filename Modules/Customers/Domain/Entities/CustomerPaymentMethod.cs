using OrderCore.Api.Shared.Domain;

namespace OrderCore.Api.Modules.Customers.Domain.Entities;

/// <summary>
/// A tokenized payment method saved on a <see cref="Customer"/>. Only the
/// non-sensitive card metadata needed to display it back to the customer is
/// kept here — the actual card data lives with <see cref="Provider"/>,
/// referenced by <see cref="ProviderCustomerReference"/> — see
/// 02-customers.md.
/// </summary>
public sealed class CustomerPaymentMethod : Entity<Guid>
{
    public string Provider { get; private set; } = string.Empty;

    public string ProviderCustomerReference { get; private set; } = string.Empty;

    public string Brand { get; private set; } = string.Empty;

    public string Last4Digits { get; private set; } = string.Empty;

    public int ExpiryMonth { get; private set; }

    public int ExpiryYear { get; private set; }

    public bool IsDefault { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    private CustomerPaymentMethod()
    {
    }

    private CustomerPaymentMethod(
        Guid id,
        string provider,
        string providerCustomerReference,
        string brand,
        string last4Digits,
        int expiryMonth,
        int expiryYear,
        DateTimeOffset now)
        : base(id)
    {
        Provider = provider;
        ProviderCustomerReference = providerCustomerReference;
        Brand = brand;
        Last4Digits = last4Digits;
        ExpiryMonth = expiryMonth;
        ExpiryYear = expiryYear;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public static CustomerPaymentMethod Create(
        string provider,
        string providerCustomerReference,
        string brand,
        string last4Digits,
        int expiryMonth,
        int expiryYear,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new ArgumentException("Provider is required.", nameof(provider));
        }

        if (string.IsNullOrWhiteSpace(providerCustomerReference))
        {
            throw new ArgumentException("Provider customer reference is required.", nameof(providerCustomerReference));
        }

        if (string.IsNullOrWhiteSpace(brand))
        {
            throw new ArgumentException("Brand is required.", nameof(brand));
        }

        if (string.IsNullOrWhiteSpace(last4Digits) || last4Digits.Length != 4)
        {
            throw new ArgumentException("Last4Digits must be exactly 4 digits.", nameof(last4Digits));
        }

        if (expiryMonth is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(expiryMonth), "Expiry month must be between 1 and 12.");
        }

        if (expiryYear < now.Year)
        {
            throw new ArgumentOutOfRangeException(nameof(expiryYear), "Expiry year cannot be in the past.");
        }

        return new CustomerPaymentMethod(
            Guid.NewGuid(), provider, providerCustomerReference, brand, last4Digits, expiryMonth, expiryYear, now);
    }

    /// <summary>
    /// Reconstructs a <see cref="CustomerPaymentMethod"/> from
    /// already-persisted state. `internal` because only
    /// <c>CustomerMapper</c> should call it — see
    /// <see cref="Customer.Rehydrate"/>.
    /// </summary>
    internal static CustomerPaymentMethod Rehydrate(
        Guid id,
        string provider,
        string providerCustomerReference,
        string brand,
        string last4Digits,
        int expiryMonth,
        int expiryYear,
        bool isDefault,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        var method = new CustomerPaymentMethod(
            id, provider, providerCustomerReference, brand, last4Digits, expiryMonth, expiryYear, createdAt)
        {
            IsDefault = isDefault,
            UpdatedAt = updatedAt,
        };

        return method;
    }

    public bool IsExpired(DateTimeOffset now) =>
        ExpiryYear < now.Year || (ExpiryYear == now.Year && ExpiryMonth < now.Month);

    public void MarkAsDefault() => IsDefault = true;

    public void UnmarkAsDefault() => IsDefault = false;
}
