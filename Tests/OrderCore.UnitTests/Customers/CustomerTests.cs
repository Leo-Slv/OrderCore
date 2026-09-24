using FluentAssertions;
using OrderCore.Api.Modules.Customers.Domain.Entities;
using OrderCore.Api.Modules.Customers.Domain.Events;
using OrderCore.Api.Shared.Domain.Exceptions;
using OrderCore.Api.Shared.Domain.ValueObjects;
using Xunit;

namespace OrderCore.UnitTests.Customers;

public sealed class CustomerTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static Customer CreateCustomer() => Customer.Create("Jane Doe", "jane@example.com", "hashed-password", Now);

    private static Address CreateAddress() => Address.Create(
        "Main St", "123", null, "Downtown", "Springfield", "IL", "62701", "USA");

    [Fact]
    public void Create_starts_active_and_raises_CustomerRegistered()
    {
        var customer = CreateCustomer();

        customer.Active.Should().BeTrue();
        customer.Addresses.Should().BeEmpty();
        customer.DomainEvents.Should().ContainSingle(e => e is CustomerRegistered);
    }

    [Fact]
    public void AddAddress_raises_CustomerAddressAdded()
    {
        var customer = CreateCustomer();
        customer.ClearDomainEvents();
        var address = CustomerAddress.Create("Home", "Jane Doe", null, CreateAddress(), Now);

        customer.AddAddress(address);

        customer.Addresses.Should().ContainSingle();
        customer.DomainEvents.Should().ContainSingle(e => e is CustomerAddressAdded);
    }

    [Fact]
    public void RemoveAddress_removes_a_known_address()
    {
        var customer = CreateCustomer();
        var address = CustomerAddress.Create("Home", "Jane Doe", null, CreateAddress(), Now);
        customer.AddAddress(address);

        customer.RemoveAddress(address.Id);

        customer.Addresses.Should().BeEmpty();
    }

    [Fact]
    public void RemoveAddress_for_unknown_id_throws()
    {
        var customer = CreateCustomer();

        var act = () => customer.RemoveAddress(Guid.NewGuid());

        act.Should().Throw<DomainRuleViolationException>();
    }

    [Fact]
    public void SetDefaultShippingAddress_unmarks_the_previous_default()
    {
        var customer = CreateCustomer();
        var first = CustomerAddress.Create("Home", "Jane Doe", null, CreateAddress(), Now);
        var second = CustomerAddress.Create("Work", "Jane Doe", null, CreateAddress(), Now);
        customer.AddAddress(first);
        customer.AddAddress(second);

        customer.SetDefaultShippingAddress(first.Id);
        customer.SetDefaultShippingAddress(second.Id);

        first.IsDefaultShipping.Should().BeFalse();
        second.IsDefaultShipping.Should().BeTrue();
    }

    [Fact]
    public void SetDefaultBillingAddress_unmarks_the_previous_default()
    {
        var customer = CreateCustomer();
        var first = CustomerAddress.Create("Home", "Jane Doe", null, CreateAddress(), Now);
        var second = CustomerAddress.Create("Work", "Jane Doe", null, CreateAddress(), Now);
        customer.AddAddress(first);
        customer.AddAddress(second);

        customer.SetDefaultBillingAddress(first.Id);
        customer.SetDefaultBillingAddress(second.Id);

        first.IsDefaultBilling.Should().BeFalse();
        second.IsDefaultBilling.Should().BeTrue();
    }

    [Fact]
    public void AddPaymentMethod_and_RemovePaymentMethod_round_trip()
    {
        var customer = CreateCustomer();
        var method = CustomerPaymentMethod.Create("stripe", "cus_123", "Visa", "4242", 12, Now.Year + 1, Now);

        customer.AddPaymentMethod(method);
        customer.PaymentMethods.Should().ContainSingle();

        customer.RemovePaymentMethod(method.Id);
        customer.PaymentMethods.Should().BeEmpty();
    }

    [Fact]
    public void Deactivate_then_Activate_toggles_Active()
    {
        var customer = CreateCustomer();

        customer.Deactivate();
        customer.Active.Should().BeFalse();

        customer.Activate();
        customer.Active.Should().BeTrue();
    }
}
