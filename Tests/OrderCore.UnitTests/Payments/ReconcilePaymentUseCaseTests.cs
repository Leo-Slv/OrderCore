using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OrderCore.Api.Modules.Payments.Application.UseCases;
using OrderCore.Api.Modules.Payments.Contracts.IntegrationEvents;
using OrderCore.Api.Modules.Payments.Domain.Entities;
using OrderCore.Api.Modules.Payments.Domain.Enums;
using OrderCore.Api.Modules.Payments.Domain.Repositories;
using OrderCore.Api.Shared.Application.Exceptions;
using Xunit;

namespace OrderCore.UnitTests.Payments;

/// <summary>
/// Reconciliation recovers what a lost webhook would have done, and leaves a
/// payment already in sync untouched.
/// </summary>
public sealed class ReconcilePaymentUseCaseTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private readonly FakePaymentRepository _payments = new();
    private readonly FakePaymentsOutbox _outbox = new();
    private readonly FakeAuditLogService _auditLog = new();
    private readonly StubPaymentProvider _provider = new() { NeedsBuyer = true };

    private ReconcilePaymentUseCase UseCase() =>
        new(
            _payments,
            _provider,
            new ApplyPaymentProviderUpdateUseCase(
                _payments, _outbox, new FakeAuditLogService(), TestMetrics.Payments, TimeProvider.System,
                NullLogger<ApplyPaymentProviderUpdateUseCase>.Instance),
            new AuthorizePaymentUseCase(_payments, _provider, _outbox, new FakeAuditLogService(), TestMetrics.Payments, TimeProvider.System),
            _auditLog,
            TestMetrics.Payments,
            TimeProvider.System);

    private async Task<Payment> WaitingPaymentAsync()
    {
        var payment = Payment.Create(Guid.NewGuid(), 70m, "BRL", PaymentMethod.Card, $"idem-{Guid.NewGuid():N}", "Stub", null, Now);
        payment.MarkProcessing();
        payment.AwaitBuyer($"pi_{payment.Id:N}", Now);
        await _payments.AddAsync(payment, CancellationToken.None);
        return payment;
    }

    [Fact]
    public async Task A_lost_authorized_webhook_is_recovered()
    {
        var payment = await WaitingPaymentAsync();
        var deadline = Now.AddDays(6);
        _provider.State = new PaymentProviderState(PaymentProviderStatus.Authorized, AuthorizationExpiresAt: deadline);

        var result = await UseCase().ExecuteAsync(payment.Id, CancellationToken.None);

        result.Changed.Should().BeTrue();
        result.StatusBefore.Should().Be("Processing");
        result.StatusAfter.Should().Be("Authorized");
        result.ProviderStatus.Should().Be("Authorized");
        payment.AuthorizationExpiresAt.Should().Be(deadline);
        _outbox.Enqueued.Should().ContainSingle().Which.Should().BeOfType<PaymentAuthorized>();
        _auditLog.Entries.Should().ContainSingle(e => e.Action == "PaymentReconciled");
    }

    [Fact]
    public async Task A_payment_already_in_sync_is_left_untouched()
    {
        var payment = await WaitingPaymentAsync();

        var result = await UseCase().ExecuteAsync(payment.Id, CancellationToken.None);

        result.Changed.Should().BeFalse();
        result.StatusAfter.Should().Be("Processing");
        _provider.StateCalls.Should().Be(1);
        _outbox.Enqueued.Should().BeEmpty();
        _auditLog.Entries.Should().BeEmpty();
        _payments.Saves.Should().Be(0);
    }

    [Fact]
    public async Task A_missed_authorization_and_capture_are_both_applied()
    {
        var payment = await WaitingPaymentAsync();
        _provider.State = new PaymentProviderState(PaymentProviderStatus.Captured);

        await UseCase().ExecuteAsync(payment.Id, CancellationToken.None);

        payment.Status.Should().Be(PaymentStatus.Captured);
        _outbox.Enqueued.Select(e => e.GetType()).Should().Equal(typeof(PaymentAuthorized), typeof(PaymentCaptured));
    }

    [Fact]
    public async Task An_authorization_that_expired_without_a_webhook_is_voided_and_announced()
    {
        var payment = await WaitingPaymentAsync();
        payment.Authorize(payment.ProviderReference!, Now.AddDays(-8));
        _provider.State = new PaymentProviderState(PaymentProviderStatus.Canceled, AuthorizationExpired: true);

        await UseCase().ExecuteAsync(payment.Id, CancellationToken.None);

        payment.Status.Should().Be(PaymentStatus.Voided);
        _outbox.Enqueued.Select(e => e.GetType()).Should().Equal(typeof(PaymentVoided), typeof(PaymentAuthorizationExpired));
    }

    [Fact]
    public async Task A_missed_decline_is_recorded()
    {
        var payment = await WaitingPaymentAsync();
        _provider.State = new PaymentProviderState(PaymentProviderStatus.WaitingForBuyer, DeclineReason: "do_not_honor");

        var result = await UseCase().ExecuteAsync(payment.Id, CancellationToken.None);

        result.Changed.Should().BeTrue();
        payment.LastDeclineReason.Should().Be("do_not_honor");
        (await UseCase().ExecuteAsync(payment.Id, CancellationToken.None)).Changed.Should().BeFalse("the decline is already recorded");
    }

    [Fact]
    public async Task A_payment_the_provider_never_answered_for_is_authorized_again_with_the_same_key()
    {
        var stuck = Payment.Create(Guid.NewGuid(), 70m, "BRL", PaymentMethod.Card, "idem-stuck", "Stub", null, Now);
        stuck.MarkProcessing();
        await _payments.AddAsync(stuck, CancellationToken.None);

        var result = await UseCase().ExecuteAsync(stuck.Id, CancellationToken.None);

        _provider.AuthorizeCalls.Should().Be(1);
        _provider.StateCalls.Should().Be(0, "there is nothing to ask the provider about yet");
        result.ProviderStatus.Should().BeNull();
        stuck.IsAwaitingBuyer.Should().BeTrue("the provider now has the payment, waiting for the buyer");
    }

    [Fact]
    public async Task An_unknown_payment_is_not_found()
    {
        var act = () => UseCase().ExecuteAsync(Guid.NewGuid(), CancellationToken.None);

        (await act.Should().ThrowAsync<NotFoundException>()).Which.Code.Should().Be("payment_not_found");
    }
}
