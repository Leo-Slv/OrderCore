using FluentAssertions;
using OrderCore.Api.Modules.Payments.Application.DTOs;
using OrderCore.Api.Modules.Payments.Application.UseCases;
using OrderCore.Api.Modules.Payments.Contracts.IntegrationEvents;
using OrderCore.Api.Modules.Payments.Domain.Entities;
using OrderCore.Api.Modules.Payments.Domain.Enums;
using OrderCore.Api.Shared.Application.Exceptions;
using OrderCore.Api.Shared.Domain.Exceptions;
using Xunit;

namespace OrderCore.UnitTests.Payments;

/// <summary>
/// A provider that needs the buyer to confirm the card (Stripe): the payment
/// waits without publishing anything, the confirmation step can be asked for
/// again, and cancelling the order cancels it at the provider.
/// </summary>
public sealed class PaymentAwaitingBuyerTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private readonly FakePaymentRepository _payments = new();
    private readonly FakePaymentsOutbox _outbox = new();
    private readonly StubPaymentProvider _provider = new() { NeedsBuyer = true };

    private CreatePaymentUseCase CreatePayment() =>
        new(_payments, _provider, _outbox, new FakeAuditLogService(), TestMetrics.Payments, TimeProvider.System);

    private static CreatePaymentCommand Command(Guid orderId) => new(orderId, 59.90m, "BRL", PaymentMethod.Card, orderId.ToString());

    private SettlePaymentForCancellationUseCase Settlement() =>
        new(
            _payments,
            _provider,
            _outbox,
            new RequestRefundUseCase(_payments, _provider, _outbox, new FakeAuditLogService(), TestMetrics.Payments, TimeProvider.System),
            new FakeAuditLogService(),
            TestMetrics.Payments,
            TimeProvider.System);

    [Fact]
    public async Task The_payment_waits_for_the_buyer_without_publishing_an_outcome()
    {
        var orderId = Guid.NewGuid();

        var result = await CreatePayment().ExecuteAsync(Command(orderId), CancellationToken.None);

        var payment = (await _payments.GetByOrderIdAsync(orderId, CancellationToken.None))!;
        payment.Status.Should().Be(PaymentStatus.Processing);
        payment.IsAwaitingBuyer.Should().BeTrue();
        payment.ProviderReference.Should().Be($"stub_ref_{payment.Id:N}");
        result.NextAction.Should().Be(new PaymentNextAction("confirm_card", StubPaymentProvider.SecretOf(payment)));
        _outbox.Enqueued.Should().BeEmpty();
    }

    [Fact]
    public async Task The_confirmation_step_is_asked_of_the_provider_again_while_the_payment_waits()
    {
        var orderId = Guid.NewGuid();
        var created = await CreatePayment().ExecuteAsync(Command(orderId), CancellationToken.None);

        var again = await new GetPaymentNextActionUseCase(_payments, _provider).ExecuteAsync(orderId, CancellationToken.None);

        again.Should().Be(created.NextAction);
    }

    [Fact]
    public async Task There_is_no_step_for_a_payment_that_no_longer_waits_or_does_not_exist()
    {
        var authorized = Payment.Create(Guid.NewGuid(), 10m, "BRL", PaymentMethod.Card, "idem-a", "Stub", null, Now);
        authorized.MarkProcessing();
        authorized.Authorize("ref", Now);
        await _payments.AddAsync(authorized, CancellationToken.None);
        var useCase = new GetPaymentNextActionUseCase(_payments, _provider);

        (await useCase.ExecuteAsync(authorized.OrderId, CancellationToken.None)).Should().BeNull();
        (await useCase.ExecuteAsync(Guid.NewGuid(), CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task Cancelling_the_order_cancels_the_waiting_payment_at_the_provider_and_voids_it()
    {
        var orderId = Guid.NewGuid();
        await CreatePayment().ExecuteAsync(Command(orderId), CancellationToken.None);

        var outcome = await Settlement().ExecuteAsync(orderId, "changed my mind", CancellationToken.None);

        outcome.Should().Be(PaymentSettlementOutcome.Voided);
        _provider.VoidCalls.Should().Be(1);
        var payment = (await _payments.GetByOrderIdAsync(orderId, CancellationToken.None))!;
        payment.Status.Should().Be(PaymentStatus.Voided);
        payment.VoidedAt.Should().NotBeNull();
        _outbox.Enqueued.Should().ContainSingle().Which.Should().BeOfType<PaymentVoided>();
        (await Settlement().ExecuteAsync(orderId, "again", CancellationToken.None)).Should().Be(PaymentSettlementOutcome.NothingToSettle);
    }

    [Fact]
    public async Task A_payment_still_with_the_provider_and_no_reference_is_still_in_progress()
    {
        var payment = Payment.Create(Guid.NewGuid(), 10m, "BRL", PaymentMethod.Card, "idem-p", "Stub", null, Now);
        payment.MarkProcessing();
        await _payments.AddAsync(payment, CancellationToken.None);

        var act = () => Settlement().ExecuteAsync(payment.OrderId, "cancel", CancellationToken.None);

        (await act.Should().ThrowAsync<ConflictException>()).Which.Code.Should().Be("payment_in_progress");
    }

    [Fact]
    public void Only_a_processing_payment_can_wait_for_the_buyer()
    {
        var payment = Payment.Create(Guid.NewGuid(), 10m, "BRL", PaymentMethod.Card, "idem-d", "Stub", null, Now);

        var act = () => payment.AwaitBuyer("pi_1", Now);

        act.Should().Throw<DomainRuleViolationException>().Which.Code.Should().Be("invalid_payment_state");
        payment.IsAwaitingBuyer.Should().BeFalse();
    }
}
