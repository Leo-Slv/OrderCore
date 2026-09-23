using OrderCore.Api.Modules.Customers.Domain.Events;
using OrderCore.Api.Shared.Domain;

namespace OrderCore.Api.Modules.Customers.Domain.Entities;

/// <summary>
/// Customer aggregate (section 5 / 02-customers.md). Owns its saved
/// addresses and payment methods: default-address/default-payment-method
/// switching always goes through the aggregate
/// (<see cref="SetDefaultShippingAddress"/> etc.) so the "unmark the
/// previous default" invariant can never be skipped by calling a child
/// entity's Mark method directly from outside.
/// </summary>
public sealed class Customer : AggregateRoot<Guid>
{
    private readonly List<CustomerAddress> _addresses = new();
    private readonly List<CustomerPaymentMethod> _paymentMethods = new();

    public string Name { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public string? Phone { get; private set; }

    public string? DocumentNumber { get; private set; }

    public string PasswordHash { get; private set; } = string.Empty;

    public bool Active { get; private set; }

    public DateTimeOffset? EmailVerifiedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<CustomerAddress> Addresses => _addresses.AsReadOnly();

    public IReadOnlyCollection<CustomerPaymentMethod> PaymentMethods => _paymentMethods.AsReadOnly();

    private Customer()
    {
    }

    private Customer(Guid id, string name, string email, string passwordHash, DateTimeOffset now) : base(id)
    {
        Name = name;
        Email = email;
        PasswordHash = passwordHash;
        Active = true;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>
    /// <paramref name="now"/> is not in 02-customers.md's abbreviated
    /// signature, but is required the same way <c>Order.Create</c> takes it
    /// (section 9): <see cref="CreatedAt"/>/<see cref="UpdatedAt"/> need a
    /// value from somewhere, and the domain must stay deterministic/testable
    /// rather than reading the clock itself.
    /// </summary>
    public static Customer Create(string name, string email, string passwordHash, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required.", nameof(email));
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException("Password hash is required.", nameof(passwordHash));
        }

        var customer = new Customer(Guid.NewGuid(), name, email, passwordHash, now);
        customer.IncrementVersion();
        customer.Raise(new CustomerRegistered(Guid.NewGuid(), now, customer.Id, email));
        return customer;
    }

    public void UpdateProfile(string name, string? phone, string? documentNumber)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        Name = name;
        Phone = phone;
        DocumentNumber = documentNumber;
        IncrementVersion();
    }

    public void VerifyEmail(DateTimeOffset now)
    {
        EmailVerifiedAt = now;
        UpdatedAt = now;
        IncrementVersion();
    }

    public void Activate()
    {
        Active = true;
        IncrementVersion();
    }

    public void Deactivate()
    {
        Active = false;
        IncrementVersion();
    }

    public void AddAddress(CustomerAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        _addresses.Add(address);
        IncrementVersion();
        // AddAddress takes no `now` (matches 02-customers.md); the address
        // is always created immediately before being added in the same use
        // case, so its own CreatedAt is an accurate OccurredAt here.
        Raise(new CustomerAddressAdded(Guid.NewGuid(), address.CreatedAt, Id, address.Id));
    }

    public void RemoveAddress(Guid addressId)
    {
        var address = FindAddress(addressId);
        _addresses.Remove(address);
        IncrementVersion();
    }

    public void SetDefaultShippingAddress(Guid addressId)
    {
        var address = FindAddress(addressId);

        foreach (var other in _addresses.Where(a => a.IsDefaultShipping))
        {
            other.UnmarkAsDefaultShipping();
        }

        address.MarkAsDefaultShipping();
        IncrementVersion();
    }

    public void SetDefaultBillingAddress(Guid addressId)
    {
        var address = FindAddress(addressId);

        foreach (var other in _addresses.Where(a => a.IsDefaultBilling))
        {
            other.UnmarkAsDefaultBilling();
        }

        address.MarkAsDefaultBilling();
        IncrementVersion();
    }

    public void AddPaymentMethod(CustomerPaymentMethod method)
    {
        ArgumentNullException.ThrowIfNull(method);

        _paymentMethods.Add(method);
        IncrementVersion();
    }

    public void RemovePaymentMethod(Guid paymentMethodId)
    {
        var method = FindPaymentMethod(paymentMethodId);
        _paymentMethods.Remove(method);
        IncrementVersion();
    }

    /// <summary>
    /// Reconstructs a <see cref="Customer"/> from already-persisted state.
    /// Distinct from <see cref="Create"/> on purpose: rehydration must not
    /// re-run creation invariants or raise <see cref="CustomerRegistered"/>
    /// again. `internal` because only <c>CustomerMapper</c> (Infrastructure,
    /// same assembly) should call it — see 02-customers.md's
    /// Domain/Mapper/PersistenceModel separation.
    /// </summary>
    internal static Customer Rehydrate(
        Guid id,
        string name,
        string email,
        string? phone,
        string? documentNumber,
        string passwordHash,
        bool active,
        DateTimeOffset? emailVerifiedAt,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        int version,
        IEnumerable<CustomerAddress> addresses,
        IEnumerable<CustomerPaymentMethod> paymentMethods)
    {
        var customer = new Customer(id, name, email, passwordHash, createdAt)
        {
            Phone = phone,
            DocumentNumber = documentNumber,
            Active = active,
            EmailVerifiedAt = emailVerifiedAt,
            UpdatedAt = updatedAt,
            Version = version,
        };

        customer._addresses.AddRange(addresses);
        customer._paymentMethods.AddRange(paymentMethods);

        return customer;
    }

    private CustomerAddress FindAddress(Guid addressId) =>
        _addresses.FirstOrDefault(a => a.Id == addressId)
            ?? throw new InvalidOperationException($"Address '{addressId}' does not belong to this customer.");

    private CustomerPaymentMethod FindPaymentMethod(Guid paymentMethodId) =>
        _paymentMethods.FirstOrDefault(m => m.Id == paymentMethodId)
            ?? throw new InvalidOperationException($"Payment method '{paymentMethodId}' does not belong to this customer.");
}
