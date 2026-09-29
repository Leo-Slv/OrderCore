using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using OrderCore.Api.Modules.Payments.Application.DTOs;
using OrderCore.Api.Modules.Payments.Application.UseCases;
using OrderCore.Api.Modules.Payments.Contracts.IntegrationEvents;
using OrderCore.Api.Modules.Payments.Domain.Entities;
using OrderCore.Api.Modules.Payments.Domain.Enums;
using Xunit;

namespace OrderCore.UnitTests.Payments;

/// <summary>
/// The 30-minute payment window and the authorization's validity, with the
/// clock under the test's control.
/// </summary>
public sealed class PaymentWindowTests
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(30);

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero));
    private readonly FakePaymentRepository _payments = new();
    private readonly FakePaymentsOutbox _outbox = new();

    private ExpirePaymentWindowUseCase UseCase(StubPaymentProvider provider) =>
        new(_payments, provider, _outbox, new FakeAuditLogService(), TestMetrics.Payments, _clock, NullLogger<ExpirePaymentWindowUseCase>.Instance);

    private async Task<Payment> WaitingPaymentAsync()
    {
        var payment = Payment.Create(Guid.NewGuid(), 50m, "BRL", PaymentMethod.Card, $"idem-{Guid.NewGuid():N}", "Stub", null, _clock.GetUtcNow());
        payment.MarkProcessing();
        payment.AwaitBuyer($"pi_{payment.Id:N}", _clock.GetUtcNow());
        await _payments.AddAsync(payment, CancellationToken.None);
        return payment;
    }

    private async Task<int> RunAsync(StubPaymentProvider provider)
    {
        var useCase = UseCase(provider);
        var expired = 0;
        foreach (var id in await useCase.FindExpiredAsync(Window, 100, CancellationToken.None))
        {
            expired += await useCase.ExpireAsync(id, Window, CancellationToken.None) ? 1 : 0;
        }

        return expired;
    }

    [Fact]
    public async Task A_payment_left_unconfirmed_for_30_minutes_is_cancelled_and_fails()
    {
        var payment = await WaitingPaymentAsync();
        var provider = new StubPaymentProvider();

        _clock.Advance(TimeSpan.FromMinutes(29));
        (await RunAsync(provider)).Should().Be(0, "the buyer still has a minute");

        _clock.Advance(TimeSpan.FromMinutes(2));
        (await RunAsync(provider)).Should().Be(1);

        provider.VoidCalls.Should().Be(1, "the intent is cancelled so the card can no longer be confirmed");
        payment.Status.Should().Be(PaymentStatus.Failed);
        payment.FailureReason.Should().Be(ExpirePaymentWindowUseCase.WindowExpiredReason);
        _outbox.Enqueued.Should().ContainSingle().Which.Should().BeOfType<PaymentFailed>()
            .Which.Reason.Should().Be("payment_window_expired");
        (await RunAsync(provider)).Should().Be(0, "a failed payment is not expired twice");
    }

    [Fact]
    public async Task The_last_decline_is_the_reason_when_the_buyer_tried_and_failed()
    {
        var payment = await WaitingPaymentAsync();
        payment.RecordDecline("insufficient_funds", _clock.GetUtcNow());

        _clock.Advance(TimeSpan.FromMinutes(31));
        await RunAsync(new StubPaymentProvider());

        payment.FailureReason.Should().Be("insufficient_funds");
    }

    [Fact]
    public async Task A_payment_the_provider_will_not_cancel_is_left_for_its_webhook()
    {
        var payment = await WaitingPaymentAsync();

        _clock.Advance(TimeSpan.FromMinutes(31));
        (await RunAsync(new StubPaymentProvider(succeeds: false))).Should().Be(0);

        payment.Status.Should().Be(PaymentStatus.Processing);
        _outbox.Enqueued.Should().BeEmpty();
    }

    [Fact]
    public async Task A_payment_stuck_before_the_provider_answered_is_left_to_reconciliation()
    {
        var stuck = Payment.Create(Guid.NewGuid(), 50m, "BRL", PaymentMethod.Card, "idem-stuck", "Stub", null, _clock.GetUtcNow());
        stuck.MarkProcessing();
        await _payments.AddAsync(stuck, CancellationToken.None);

        _clock.Advance(TimeSpan.FromHours(1));
        (await RunAsync(new StubPaymentProvider())).Should().Be(0);

        stuck.Status.Should().Be(PaymentStatus.Processing);
    }

    [Fact]
    public async Task An_authorization_lasts_the_providers_deadline_or_seven_days()
    {
        var withDeadline = await WaitingPaymentAsync();
        var withoutDeadline = await WaitingPaymentAsync();
        var deadline = _clock.GetUtcNow().AddDays(5);
        var apply = new ApplyPaymentProviderUpdateUseCase(
            _payments, _outbox, new FakeAuditLogService(), TestMetrics.Payments, _clock, NullLogger<ApplyPaymentProviderUpdateUseCase>.Instance);

        await apply.ExecuteAsync(
            new PaymentProviderUpdate(PaymentProviderUpdateKind.Authorized, withDeadline.ProviderReference!, _clock.GetUtcNow(), AuthorizationExpiresAt: deadline),
            CancellationToken.None);
        await apply.ExecuteAsync(
            new PaymentProviderUpdate(PaymentProviderUpdateKind.Authorized, withoutDeadline.ProviderReference!, _clock.GetUtcNow()),
            CancellationToken.None);

        withDeadline.AuthorizationExpiresAt.Should().Be(deadline);
        withoutDeadline.AuthorizationExpiresAt.Should().Be(_clock.GetUtcNow().AddDays(7));
        (await new CountExpiringAuthorizationsUseCase(_payments).ExecuteAsync(_clock.GetUtcNow().AddDays(6), CancellationToken.None))
            .Should().Be(1);
    }
}
