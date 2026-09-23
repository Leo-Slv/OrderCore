using FluentAssertions;
using OrderCore.Api.Modules.Payments.Domain.Entities;
using OrderCore.Api.Modules.Payments.Domain.Enums;
using Xunit;

namespace OrderCore.UnitTests.Payments;

public sealed class RefundTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static Payment CreateCapturedPayment(decimal amount = 100m)
    {
        var payment = Payment.Create(Guid.NewGuid(), amount, "BRL", "idem-key-1", "Fake", customerPaymentMethodId: null, Now);
        payment.MarkProcessing();
        payment.Authorize("provider-ref", Now);
        payment.Capture(Now);
        return payment;
    }

    [Fact]
    public void Complete_sets_ProcessedAt()
    {
        var refund = CreateCapturedPayment().RequestRefund(10m, "reason", Now);

        refund.Complete(Now);

        refund.Status.Should().Be(RefundStatus.Completed);
        refund.ProcessedAt.Should().Be(Now);
    }

    [Fact]
    public void Complete_twice_throws()
    {
        var refund = CreateCapturedPayment().RequestRefund(10m, "reason", Now);
        refund.Complete(Now);

        var act = () => refund.Complete(Now);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Fail_after_Complete_throws()
    {
        var refund = CreateCapturedPayment().RequestRefund(10m, "reason", Now);
        refund.Complete(Now);

        var act = () => refund.Fail("provider declined", Now);

        act.Should().Throw<InvalidOperationException>();
    }
}
