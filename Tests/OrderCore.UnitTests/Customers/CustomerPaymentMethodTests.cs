using FluentAssertions;
using OrderCore.Api.Modules.Customers.Domain.Entities;
using Xunit;

namespace OrderCore.UnitTests.Customers;

public sealed class CustomerPaymentMethodTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IsExpired_is_false_for_a_future_expiry()
    {
        var method = CustomerPaymentMethod.Create("stripe", "cus_123", "Visa", "4242", 12, 2027, Now);

        method.IsExpired(Now).Should().BeFalse();
    }

    [Fact]
    public void IsExpired_is_true_once_the_expiry_month_has_passed()
    {
        var method = CustomerPaymentMethod.Create("stripe", "cus_123", "Visa", "4242", 12, 2027, Now);
        var later = new DateTimeOffset(2028, 1, 1, 0, 0, 0, TimeSpan.Zero);

        method.IsExpired(later).Should().BeTrue();
    }

    [Fact]
    public void Create_rejects_an_expiry_year_in_the_past()
    {
        var act = () => CustomerPaymentMethod.Create("stripe", "cus_123", "Visa", "4242", 12, 2025, Now);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void MarkAsDefault_then_UnmarkAsDefault_toggles_IsDefault()
    {
        var method = CustomerPaymentMethod.Create("stripe", "cus_123", "Visa", "4242", 12, 2027, Now);

        method.MarkAsDefault();
        method.IsDefault.Should().BeTrue();

        method.UnmarkAsDefault();
        method.IsDefault.Should().BeFalse();
    }
}
