using FluentAssertions;
using OrderCore.Api.Modules.Payments.Application.DTOs;
using OrderCore.Api.Modules.Payments.Application.UseCases;
using OrderCore.Api.Modules.Payments.Domain.Entities;
using OrderCore.Api.Modules.Payments.Domain.Enums;
using OrderCore.Api.Shared.Application.Exceptions;
using OrderCore.Api.Shared.Domain.Exceptions;
using Xunit;

namespace OrderCore.UnitTests.Payments;

/// <summary>
/// Capture on shipping and settlement on cancellation (backoffice
/// decisions 1 and 2): every branch, and that repeating either is a no-op
/// that never goes back to the provider.
/// </summary>
public sealed class SettlementAndCaptureUseCaseTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private readonly FakePaymentRepository _payments = new();

    private async Task<Payment> SavePaymentAsync(PaymentStatus status, decimal amount = 100m)
    {
        var payment = Payment.Create(Guid.NewGuid(), amount, "BRL", PaymentMethod.Card, $"idem-{Guid.NewGuid():N}", "Fake", null, Now);

        if (status != PaymentStatus.Pending)
        {
            payment.MarkProcessing();
        }

        switch (status)
        {
            case PaymentStatus.Authorized:
                payment.Authorize("ref", Now);
                break;
            case PaymentStatus.Captured:
                payment.Authorize("ref", Now);
                payment.Capture(Now);
                break;
            case PaymentStatus.Voided:
                payment.Authorize("ref", Now);
                payment.Void(Now);
                break;
            case PaymentStatus.Refunded:
                payment.Authorize("ref", Now);
                payment.Capture(Now);
                payment.RequestRefund(amount, "earlier", Now).Complete(Now);
                payment.Refund();
                break;
            case PaymentStatus.Failed:
                payment.Fail("card_declined");
                break;
        }

        await _payments.AddAsync(payment, CancellationToken.None);
        return payment;
    }

    private SettlePaymentForCancellationUseCase Settlement(StubPaymentProvider provider) =>
        new(
            _payments,
            provider,
            new RequestRefundUseCase(_payments, provider, new FakeOutboxWriter(), new FakeAuditLogService(), TimeProvider.System),
            new FakeAuditLogService(),
            TimeProvider.System);

    private CapturePaymentUseCase Capture(StubPaymentProvider provider) =>
        new(_payments, provider, new FakeAuditLogService(), TimeProvider.System);

    [Fact]
    public async Task Settling_an_authorized_payment_voids_it()
    {
        var payment = await SavePaymentAsync(PaymentStatus.Authorized);
        var provider = new StubPaymentProvider();

        var outcome = await Settlement(provider).ExecuteAsync(payment.OrderId, "customer changed their mind", CancellationToken.None);

        outcome.Should().Be(PaymentSettlementOutcome.Voided);
        payment.Status.Should().Be(PaymentStatus.Voided);
        provider.VoidCalls.Should().Be(1);
    }

    [Fact]
    public async Task Settling_a_captured_payment_refunds_it_in_full()
    {
        var payment = await SavePaymentAsync(PaymentStatus.Captured, amount: 80m);
        var provider = new StubPaymentProvider();

        var outcome = await Settlement(provider).ExecuteAsync(payment.OrderId, "out of stock", CancellationToken.None);

        outcome.Should().Be(PaymentSettlementOutcome.Refunded);
        payment.Status.Should().Be(PaymentStatus.Refunded);
        payment.Refunds.Should().ContainSingle().Which.Amount.Should().Be(80m);
    }

    [Fact]
    public async Task Settling_a_partly_refunded_payment_refunds_only_the_rest()
    {
        var payment = await SavePaymentAsync(PaymentStatus.Captured, amount: 100m);
        payment.RequestRefund(30m, "damaged item", Now).Complete(Now);

        await Settlement(new StubPaymentProvider()).ExecuteAsync(payment.OrderId, "cancelled", CancellationToken.None);

        payment.Status.Should().Be(PaymentStatus.Refunded);
        payment.Refunds.Select(r => r.Amount).Should().Equal(30m, 70m);
    }

    [Theory]
    [InlineData(PaymentStatus.Failed)]
    [InlineData(PaymentStatus.Voided)]
    [InlineData(PaymentStatus.Refunded)]
    public async Task Settling_a_payment_that_holds_no_money_does_nothing(PaymentStatus status)
    {
        var payment = await SavePaymentAsync(status);
        var provider = new StubPaymentProvider();

        var outcome = await Settlement(provider).ExecuteAsync(payment.OrderId, "cancelled", CancellationToken.None);

        outcome.Should().Be(PaymentSettlementOutcome.NothingToSettle);
        payment.Status.Should().Be(status);
        (provider.VoidCalls + provider.RefundCalls).Should().Be(0);
    }

    [Fact]
    public async Task Settling_an_order_without_a_payment_does_nothing()
    {
        var outcome = await Settlement(new StubPaymentProvider()).ExecuteAsync(Guid.NewGuid(), "cancelled", CancellationToken.None);

        outcome.Should().Be(PaymentSettlementOutcome.NothingToSettle);
    }

    [Theory]
    [InlineData(PaymentStatus.Pending)]
    [InlineData(PaymentStatus.Processing)]
    public async Task Settling_a_payment_still_with_the_provider_is_a_conflict(PaymentStatus status)
    {
        var payment = await SavePaymentAsync(status);

        var act = () => Settlement(new StubPaymentProvider()).ExecuteAsync(payment.OrderId, "cancelled", CancellationToken.None);

        (await act.Should().ThrowAsync<ConflictException>()).Which.Code.Should().Be("payment_in_progress");
    }

    [Fact]
    public async Task A_refused_void_is_a_conflict_and_leaves_the_payment_authorized()
    {
        var payment = await SavePaymentAsync(PaymentStatus.Authorized);

        var act = () => Settlement(new StubPaymentProvider(succeeds: false)).ExecuteAsync(payment.OrderId, "cancelled", CancellationToken.None);

        (await act.Should().ThrowAsync<ConflictException>()).Which.Code.Should().Be("payment_void_failed");
        payment.Status.Should().Be(PaymentStatus.Authorized);
    }

    [Fact]
    public async Task A_refused_refund_is_a_conflict_and_leaves_the_payment_captured()
    {
        var payment = await SavePaymentAsync(PaymentStatus.Captured);

        var act = () => Settlement(new StubPaymentProvider(succeeds: false)).ExecuteAsync(payment.OrderId, "cancelled", CancellationToken.None);

        (await act.Should().ThrowAsync<ConflictException>()).Which.Code.Should().Be("payment_refund_failed");
        payment.Status.Should().Be(PaymentStatus.Captured);
    }

    [Fact]
    public async Task Capturing_an_orders_payment_captures_it()
    {
        var payment = await SavePaymentAsync(PaymentStatus.Authorized);

        await Capture(new StubPaymentProvider()).ExecuteForOrderAsync(payment.OrderId, CancellationToken.None);

        payment.Status.Should().Be(PaymentStatus.Captured);
    }

    [Fact]
    public async Task Capturing_again_is_a_no_op_that_skips_the_provider()
    {
        var payment = await SavePaymentAsync(PaymentStatus.Captured);
        var provider = new StubPaymentProvider();

        var result = await Capture(provider).ExecuteForOrderAsync(payment.OrderId, CancellationToken.None);

        result.Status.Should().Be("Captured");
        provider.CaptureCalls.Should().Be(0);
    }

    [Fact]
    public async Task A_refused_capture_is_a_conflict_and_leaves_the_payment_authorized()
    {
        var payment = await SavePaymentAsync(PaymentStatus.Authorized);

        var act = () => Capture(new StubPaymentProvider(succeeds: false)).ExecuteForOrderAsync(payment.OrderId, CancellationToken.None);

        (await act.Should().ThrowAsync<ConflictException>()).Which.Code.Should().Be("payment_capture_failed");
        payment.Status.Should().Be(PaymentStatus.Authorized);
    }

    [Fact]
    public async Task A_voided_payment_is_never_sent_to_the_provider_for_capture()
    {
        var payment = await SavePaymentAsync(PaymentStatus.Voided);
        var provider = new StubPaymentProvider();

        var act = () => Capture(provider).ExecuteForOrderAsync(payment.OrderId, CancellationToken.None);

        (await act.Should().ThrowAsync<DomainRuleViolationException>()).Which.Code.Should().Be("invalid_payment_state");
        provider.CaptureCalls.Should().Be(0);
    }

    [Fact]
    public async Task Capturing_for_an_order_without_a_payment_is_not_found()
    {
        var act = () => Capture(new StubPaymentProvider()).ExecuteForOrderAsync(Guid.NewGuid(), CancellationToken.None);

        (await act.Should().ThrowAsync<NotFoundException>()).Which.Code.Should().Be("payment_not_found");
    }
}
