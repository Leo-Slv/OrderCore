using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OrderCore.Api.Modules.Payments.Application.DTOs;
using OrderCore.Api.Modules.Payments.Application.UseCases;
using OrderCore.Api.Modules.Payments.Contracts.IntegrationEvents;
using OrderCore.Api.Modules.Payments.Domain.Entities;
using OrderCore.Api.Modules.Payments.Domain.Enums;
using Xunit;
using Kind = OrderCore.Api.Modules.Payments.Application.DTOs.PaymentProviderUpdateKind;

namespace OrderCore.UnitTests.Payments;

/// <summary>
/// What each thing the provider says does to a payment, and that an update
/// arriving late, twice or out of order changes nothing.
/// </summary>
public sealed class ApplyPaymentProviderUpdateUseCaseTests
{
    private const string Intent = "pi_123";
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private readonly FakePaymentRepository _payments = new();
    private readonly FakePaymentsOutbox _outbox = new();

    private ApplyPaymentProviderUpdateUseCase UseCase() =>
        new(_payments, _outbox, new FakeAuditLogService(), TestMetrics.Payments, TimeProvider.System, NullLogger<ApplyPaymentProviderUpdateUseCase>.Instance);

    private async Task<Payment> WaitingPaymentAsync()
    {
        var payment = Payment.Create(Guid.NewGuid(), 80m, "BRL", PaymentMethod.Card, $"idem-{Guid.NewGuid():N}", "Stripe", null, Now);
        payment.MarkProcessing();
        payment.AwaitBuyer(Intent, Now);
        await _payments.AddAsync(payment, CancellationToken.None);
        return payment;
    }

    private async Task<Payment> AuthorizedPaymentAsync()
    {
        var payment = await WaitingPaymentAsync();
        payment.Authorize(Intent, Now);
        return payment;
    }

    private Task<PaymentProviderUpdateOutcome> Apply(Kind kind, string? reason = null, Guid? refundId = null, bool expired = false) =>
        UseCase().ExecuteAsync(new PaymentProviderUpdate(kind, Intent, Now, reason, refundId, expired), CancellationToken.None);

    [Fact]
    public async Task Authorized_authorizes_a_waiting_payment_and_lets_orders_know()
    {
        var payment = await WaitingPaymentAsync();

        (await Apply(Kind.Authorized)).Should().Be(PaymentProviderUpdateOutcome.Applied);

        payment.Status.Should().Be(PaymentStatus.Authorized);
        _outbox.Enqueued.Should().ContainSingle().Which.Should().BeOfType<PaymentAuthorized>()
            .Which.OrderId.Should().Be(payment.OrderId);
    }

    [Fact]
    public async Task A_repeated_or_late_authorized_changes_nothing()
    {
        var payment = await AuthorizedPaymentAsync();
        payment.Void(Now);

        (await Apply(Kind.Authorized)).Should().Be(PaymentProviderUpdateOutcome.Ignored);

        payment.Status.Should().Be(PaymentStatus.Voided);
        _outbox.Enqueued.Should().BeEmpty();
        _payments.Saves.Should().Be(0);
    }

    [Fact]
    public async Task A_decline_is_recorded_and_the_payment_keeps_waiting_for_the_buyer()
    {
        var payment = await WaitingPaymentAsync();

        await Apply(Kind.Declined, reason: "insufficient_funds");

        payment.Status.Should().Be(PaymentStatus.Processing);
        payment.IsAwaitingBuyer.Should().BeTrue();
        payment.LastDeclineReason.Should().Be("insufficient_funds");
        payment.LastDeclinedAt.Should().Be(Now);
        _outbox.Enqueued.Should().BeEmpty("a decline isn't the end; the buyer may try another card");

        await Apply(Kind.Authorized);
        payment.Status.Should().Be(PaymentStatus.Authorized, "a later card went through");
    }

    [Fact]
    public async Task Captured_at_the_provider_captures_an_authorized_payment_once()
    {
        var payment = await AuthorizedPaymentAsync();

        (await Apply(Kind.Captured)).Should().Be(PaymentProviderUpdateOutcome.Applied);
        (await Apply(Kind.Captured)).Should().Be(PaymentProviderUpdateOutcome.Ignored);

        payment.Status.Should().Be(PaymentStatus.Captured);
        _outbox.Enqueued.Should().ContainSingle().Which.Should().BeOfType<PaymentCaptured>();
    }

    [Fact]
    public async Task An_expired_authorization_voids_the_payment_and_says_it_expired()
    {
        var payment = await AuthorizedPaymentAsync();

        await Apply(Kind.Canceled, expired: true);

        payment.Status.Should().Be(PaymentStatus.Voided);
        _outbox.Enqueued.Select(e => e.GetType()).Should().Equal(typeof(PaymentVoided), typeof(PaymentAuthorizationExpired));
    }

    [Fact]
    public async Task An_authorization_cancelled_for_another_reason_is_only_voided()
    {
        var payment = await AuthorizedPaymentAsync();

        await Apply(Kind.Canceled);

        payment.Status.Should().Be(PaymentStatus.Voided);
        _outbox.Enqueued.Should().ContainSingle().Which.Should().BeOfType<PaymentVoided>();
    }

    [Fact]
    public async Task A_waiting_payment_cancelled_at_the_provider_fails_with_the_last_decline()
    {
        var payment = await WaitingPaymentAsync();
        await Apply(Kind.Declined, reason: "card_declined");

        await Apply(Kind.Canceled);

        payment.Status.Should().Be(PaymentStatus.Failed);
        payment.FailureReason.Should().Be("card_declined");
        _outbox.Enqueued.Should().ContainSingle().Which.Should().BeOfType<PaymentFailed>()
            .Which.Reason.Should().Be("card_declined");
    }

    [Fact]
    public async Task A_pending_refund_is_settled_and_a_full_one_refunds_the_payment()
    {
        var payment = await AuthorizedPaymentAsync();
        payment.Capture(Now);
        var refund = payment.RequestRefund(80m, "damaged", Now);

        (await Apply(Kind.RefundSucceeded, refundId: refund.Id)).Should().Be(PaymentProviderUpdateOutcome.Applied);
        (await Apply(Kind.RefundSucceeded, refundId: refund.Id)).Should().Be(PaymentProviderUpdateOutcome.Ignored);

        refund.Status.Should().Be(RefundStatus.Completed);
        payment.Status.Should().Be(PaymentStatus.Refunded);
        _outbox.Enqueued.Should().ContainSingle().Which.Should().BeOfType<PaymentRefunded>();
    }

    [Fact]
    public async Task A_failed_refund_is_recorded_with_the_providers_reason()
    {
        var payment = await AuthorizedPaymentAsync();
        payment.Capture(Now);
        var refund = payment.RequestRefund(30m, "late", Now);

        await Apply(Kind.RefundFailed, reason: "expired_or_canceled_card", refundId: refund.Id);

        refund.Status.Should().Be(RefundStatus.Failed);
        refund.Reason.Should().Be("expired_or_canceled_card");
        payment.Status.Should().Be(PaymentStatus.Captured);
        _outbox.Enqueued.Should().BeEmpty();
    }

    [Fact]
    public async Task A_dispute_is_recorded_once()
    {
        var payment = await AuthorizedPaymentAsync();
        payment.Capture(Now);

        (await Apply(Kind.DisputeOpened, reason: "fraudulent")).Should().Be(PaymentProviderUpdateOutcome.Applied);
        (await Apply(Kind.DisputeOpened, reason: "fraudulent")).Should().Be(PaymentProviderUpdateOutcome.Ignored);

        payment.IsDisputed.Should().BeTrue();
        payment.DisputedAt.Should().Be(Now);
    }

    [Fact]
    public async Task An_update_for_a_payment_orderCore_does_not_know_is_ignored()
    {
        (await Apply(Kind.Authorized)).Should().Be(PaymentProviderUpdateOutcome.UnknownPayment);

        _outbox.Enqueued.Should().BeEmpty();
    }
}
