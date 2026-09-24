using FluentAssertions;
using OrderCore.Api.Modules.Payments.Domain.Entities;
using OrderCore.Api.Modules.Payments.Domain.Enums;
using OrderCore.Api.Shared.Domain.Exceptions;
using Xunit;

namespace OrderCore.UnitTests.Payments;

public sealed class PaymentTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static Payment CreatePayment(decimal amount = 100m) =>
        Payment.Create(Guid.NewGuid(), amount, "BRL", PaymentMethod.Card, "idem-key-1", "Fake", customerPaymentMethodId: null, Now);

    private static Payment CreateCapturedPayment(decimal amount = 100m)
    {
        var payment = CreatePayment(amount);
        payment.MarkProcessing();
        payment.Authorize("provider-ref", Now);
        payment.Capture(Now);
        return payment;
    }

    [Fact]
    public void Create_records_the_payment_method()
    {
        var payment = Payment.Create(Guid.NewGuid(), 100m, "BRL", PaymentMethod.Pix, "idem-key-1", "Fake", customerPaymentMethodId: null, Now);

        payment.Method.Should().Be(PaymentMethod.Pix);
    }

    [Fact]
    public void Create_rejects_an_undefined_payment_method()
    {
        var act = () => Payment.Create(Guid.NewGuid(), 100m, "BRL", (PaymentMethod)99, "idem-key-1", "Fake", customerPaymentMethodId: null, Now);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_starts_in_Pending_status()
    {
        var payment = CreatePayment();

        payment.Status.Should().Be(PaymentStatus.Pending);
    }

    [Fact]
    public void Authorize_sets_AuthorizedAt_and_ProviderReference()
    {
        var payment = CreatePayment();
        payment.MarkProcessing();

        payment.Authorize("provider-ref", Now);

        payment.Status.Should().Be(PaymentStatus.Authorized);
        payment.AuthorizedAt.Should().Be(Now);
        payment.ProviderReference.Should().Be("provider-ref");
    }

    [Fact]
    public void Capture_sets_CapturedAt()
    {
        var payment = CreatePayment();
        payment.MarkProcessing();
        payment.Authorize("provider-ref", Now);

        payment.Capture(Now);

        payment.Status.Should().Be(PaymentStatus.Captured);
        payment.CapturedAt.Should().Be(Now);
    }

    [Fact]
    public void Fail_after_Captured_throws()
    {
        var payment = CreateCapturedPayment();

        var act = () => payment.Fail("too_late");

        act.Should().Throw<DomainRuleViolationException>();
    }

    [Fact]
    public void RequestRefund_for_the_full_amount_succeeds()
    {
        var payment = CreateCapturedPayment(100m);

        var refund = payment.RequestRefund(100m, "customer request", Now);

        payment.Refunds.Should().ContainSingle();
        refund.Amount.Should().Be(100m);
    }

    [Fact]
    public void RequestRefund_exceeding_the_refundable_balance_throws()
    {
        var payment = CreateCapturedPayment(100m);
        payment.RequestRefund(60m, "partial refund", Now);

        var act = () => payment.RequestRefund(41m, "another refund", Now);

        act.Should().Throw<DomainRuleViolationException>();
    }

    [Fact]
    public void RequestRefund_ignores_already_failed_refunds_when_computing_balance()
    {
        var payment = CreateCapturedPayment(100m);
        var firstRefund = payment.RequestRefund(60m, "will fail", Now);
        firstRefund.Fail("provider declined", Now);

        var act = () => payment.RequestRefund(100m, "retry", Now);

        act.Should().NotThrow();
    }

    [Fact]
    public void RequestRefund_before_Captured_throws()
    {
        var payment = CreatePayment();

        var act = () => payment.RequestRefund(10m, "too early", Now);

        act.Should().Throw<DomainRuleViolationException>();
    }
}
