using FluentAssertions;
using OrderCore.Api.Modules.Payments.Application.DTOs;
using OrderCore.Api.Modules.Payments.Application.UseCases;
using OrderCore.Api.Modules.Payments.Domain.Entities;
using OrderCore.Api.Modules.Payments.Domain.Enums;
using Xunit;

namespace OrderCore.UnitTests.Payments;

public sealed class RequestRefundUseCaseTests
{
    private static async Task<(FakePaymentRepository Payments, Payment Payment)> SetupCapturedPaymentAsync(decimal amount = 100m)
    {
        var payments = new FakePaymentRepository();
        var payment = Payment.Create(Guid.NewGuid(), amount, "BRL", "idem-1", "Fake", null, DateTimeOffset.UtcNow);
        payment.MarkProcessing();
        payment.Authorize("ref", DateTimeOffset.UtcNow);
        payment.Capture(DateTimeOffset.UtcNow);
        await payments.AddAsync(payment, CancellationToken.None);
        return (payments, payment);
    }

    [Fact]
    public async Task ExecuteAsync_full_refund_marks_the_payment_Refunded()
    {
        var (payments, payment) = await SetupCapturedPaymentAsync(100m);
        var outbox = new FakeOutboxWriter();
        var useCase = new RequestRefundUseCase(payments, new StubPaymentProvider(succeeds: true), outbox, TimeProvider.System);

        await useCase.ExecuteAsync(new RequestRefundCommand(payment.Id, 100m, "customer request"), CancellationToken.None);

        payment.Status.Should().Be(PaymentStatus.Refunded);
        outbox.Enqueued.Should().ContainSingle();
    }

    [Fact]
    public async Task ExecuteAsync_partial_refund_does_not_mark_the_payment_Refunded()
    {
        var (payments, payment) = await SetupCapturedPaymentAsync(100m);
        var useCase = new RequestRefundUseCase(payments, new StubPaymentProvider(succeeds: true), new FakeOutboxWriter(), TimeProvider.System);

        await useCase.ExecuteAsync(new RequestRefundCommand(payment.Id, 40m, "partial"), CancellationToken.None);

        payment.Status.Should().Be(PaymentStatus.Captured);
    }

    [Fact]
    public async Task ExecuteAsync_when_the_provider_declines_marks_the_refund_Failed_without_touching_payment_status()
    {
        var (payments, payment) = await SetupCapturedPaymentAsync(100m);
        var useCase = new RequestRefundUseCase(payments, new StubPaymentProvider(succeeds: false), new FakeOutboxWriter(), TimeProvider.System);

        await useCase.ExecuteAsync(new RequestRefundCommand(payment.Id, 40m, "partial"), CancellationToken.None);

        payment.Refunds.Single().Status.Should().Be(RefundStatus.Failed);
        payment.Status.Should().Be(PaymentStatus.Captured);
    }
}
