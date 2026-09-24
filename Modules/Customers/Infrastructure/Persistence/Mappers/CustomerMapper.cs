using OrderCore.Api.Modules.Customers.Domain.Entities;
using OrderCore.Api.Modules.Customers.Infrastructure.Persistence.Models;
using OrderCore.Api.Shared.Domain.ValueObjects;
using OrderCore.Api.Shared.Infrastructure.Persistence;

namespace OrderCore.Api.Modules.Customers.Infrastructure.Persistence.Mappers;

/// <summary>
/// Translates between the <see cref="Customer"/> aggregate and its
/// persistence models (section on Persistence / 02-customers.md).
/// <see cref="ToDomain"/> goes through <c>Customer.Rehydrate</c>, not
/// <c>Customer.Create</c>, so loading an existing customer never re-raises
/// <c>CustomerRegistered</c>.
/// </summary>
public static class CustomerMapper
{
    public static Customer ToDomain(CustomerPersistenceModel model)
    {
        var addresses = model.Addresses.Select(ToDomain);
        var paymentMethods = model.PaymentMethods.Select(ToDomain);

        return Customer.Rehydrate(
            model.Id,
            model.Name,
            model.Email,
            model.Phone,
            model.DocumentNumber,
            model.Active,
            model.EmailVerifiedAt,
            model.CreatedAt,
            model.UpdatedAt,
            model.Version,
            addresses,
            paymentMethods);
    }

    public static CustomerPersistenceModel ToPersistence(Customer domain) => new()
    {
        Id = domain.Id,
        Name = domain.Name,
        Email = domain.Email,
        Phone = domain.Phone,
        DocumentNumber = domain.DocumentNumber,
        Active = domain.Active,
        EmailVerifiedAt = domain.EmailVerifiedAt,
        CreatedAt = domain.CreatedAt,
        UpdatedAt = domain.UpdatedAt,
        Version = domain.Version,
        Addresses = domain.Addresses.Select(ToPersistence).ToList(),
        PaymentMethods = domain.PaymentMethods.Select(ToPersistence).ToList(),
    };

    /// <summary>
    /// Applies the current state of an already-tracked <paramref name="domain"/>
    /// aggregate onto its persistence model in place, instead of replacing
    /// the tracked graph — EF Core needs this to tell inserts, updates and
    /// deletes of child addresses/payment methods apart.
    /// </summary>
    public static void ApplyChanges(Customer domain, CustomerPersistenceModel model)
    {
        model.Name = domain.Name;
        model.Email = domain.Email;
        model.Phone = domain.Phone;
        model.DocumentNumber = domain.DocumentNumber;
        model.Active = domain.Active;
        model.EmailVerifiedAt = domain.EmailVerifiedAt;
        model.UpdatedAt = domain.UpdatedAt;
        model.Version = domain.Version;

        ChildCollectionReconciler.Reconcile(domain.Addresses, model.Addresses, ToPersistence, ApplyChanges, a => a.Id);
        ChildCollectionReconciler.Reconcile(domain.PaymentMethods, model.PaymentMethods, ToPersistence, ApplyChanges, m => m.Id);
    }

    private static CustomerAddressPersistenceModel ToPersistence(CustomerAddress domain) => new()
    {
        Id = domain.Id,
        Label = domain.Label,
        RecipientName = domain.RecipientName,
        Phone = domain.Phone,
        Street = domain.Address.Street,
        Number = domain.Address.Number,
        Complement = domain.Address.Complement,
        Neighborhood = domain.Address.Neighborhood,
        City = domain.Address.City,
        State = domain.Address.State,
        PostalCode = domain.Address.PostalCode,
        Country = domain.Address.Country,
        IsDefaultShipping = domain.IsDefaultShipping,
        IsDefaultBilling = domain.IsDefaultBilling,
        CreatedAt = domain.CreatedAt,
        UpdatedAt = domain.UpdatedAt,
    };

    private static void ApplyChanges(CustomerAddress domain, CustomerAddressPersistenceModel model)
    {
        model.Label = domain.Label;
        model.RecipientName = domain.RecipientName;
        model.Phone = domain.Phone;
        model.Street = domain.Address.Street;
        model.Number = domain.Address.Number;
        model.Complement = domain.Address.Complement;
        model.Neighborhood = domain.Address.Neighborhood;
        model.City = domain.Address.City;
        model.State = domain.Address.State;
        model.PostalCode = domain.Address.PostalCode;
        model.Country = domain.Address.Country;
        model.IsDefaultShipping = domain.IsDefaultShipping;
        model.IsDefaultBilling = domain.IsDefaultBilling;
        model.UpdatedAt = domain.UpdatedAt;
    }

    private static CustomerAddress ToDomain(CustomerAddressPersistenceModel model) => CustomerAddress.Rehydrate(
        model.Id,
        model.Label,
        model.RecipientName,
        model.Phone,
        Address.Create(
            model.Street, model.Number, model.Complement, model.Neighborhood, model.City, model.State, model.PostalCode, model.Country),
        model.IsDefaultShipping,
        model.IsDefaultBilling,
        model.CreatedAt,
        model.UpdatedAt);

    private static CustomerPaymentMethodPersistenceModel ToPersistence(CustomerPaymentMethod domain) => new()
    {
        Id = domain.Id,
        Provider = domain.Provider,
        ProviderCustomerReference = domain.ProviderCustomerReference,
        Brand = domain.Brand,
        Last4Digits = domain.Last4Digits,
        ExpiryMonth = domain.ExpiryMonth,
        ExpiryYear = domain.ExpiryYear,
        IsDefault = domain.IsDefault,
        CreatedAt = domain.CreatedAt,
        UpdatedAt = domain.UpdatedAt,
    };

    private static void ApplyChanges(CustomerPaymentMethod domain, CustomerPaymentMethodPersistenceModel model)
    {
        model.IsDefault = domain.IsDefault;
        model.UpdatedAt = domain.UpdatedAt;
    }

    private static CustomerPaymentMethod ToDomain(CustomerPaymentMethodPersistenceModel model) => CustomerPaymentMethod.Rehydrate(
        model.Id,
        model.Provider,
        model.ProviderCustomerReference,
        model.Brand,
        model.Last4Digits,
        model.ExpiryMonth,
        model.ExpiryYear,
        model.IsDefault,
        model.CreatedAt,
        model.UpdatedAt);
}
