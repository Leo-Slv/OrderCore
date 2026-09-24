using FluentAssertions;
using OrderCore.Api.Modules.Payments.Application.Contracts.IntegrationEvents;
using OrderCore.Api.Modules.Payments.Application.DTOs;
using OrderCore.Api.Modules.Payments.Application.UseCases;
using OrderCore.Api.Modules.Payments.Domain.Enums;
using Xunit;

namespace OrderCore.UnitTests.Payments;

public sealed class CreatePaymentUseCaseTests
{
    private static CreatePaymentCommand Command(PaymentMethod method = PaymentMethod.Card) =>
        new(Guid.NewGuid(), 100m, "BRL", method, "idem-1");

    [Fact]
    public async Task ExecuteAsync_authorizes_and_enqueues_PaymentAuthorized_when_the_provider_succeeds()
    {
        var outbox = new FakeOutboxWriter();
        var useCase = new CreatePaymentUseCase(new FakePaymentRepository(), new StubPaymentProvider(succeeds: true), outbox, new FakeAuditLogService(), TimeProvider.System);

        var result = await useCase.ExecuteAsync(Command(), CancellationToken.None);

        result.Status.Should().Be(PaymentStatus.Authorized.ToString());
        outbox.Enqueued.Should().ContainSingle().Which.Should().BeOfType<PaymentAuthorized>();
    }

    [Fact]
    public async Task ExecuteAsync_fails_and_enqueues_PaymentFailed_when_the_provider_declines()
    {
        var outbox = new FakeOutboxWriter();
        var useCase = new CreatePaymentUseCase(new FakePaymentRepository(), new StubPaymentProvider(succeeds: false), outbox, new FakeAuditLogService(), TimeProvider.System);

        var result = await useCase.ExecuteAsync(Command(), CancellationToken.None);

        result.Status.Should().Be(PaymentStatus.Failed.ToString());
        outbox.Enqueued.Should().ContainSingle().Which.Should().BeOfType<PaymentFailed>();
    }

    [Fact]
    public async Task ExecuteAsync_records_the_chosen_payment_method()
    {
        var payments = new FakePaymentRepository();
        var useCase = new CreatePaymentUseCase(payments, new StubPaymentProvider(succeeds: true), new FakeOutboxWriter(), new FakeAuditLogService(), TimeProvider.System);
        var command = Command(PaymentMethod.Pix);

        await useCase.ExecuteAsync(command, CancellationToken.None);

        var payment = await payments.GetByOrderIdAsync(command.OrderId, CancellationToken.None);
        payment!.Method.Should().Be(PaymentMethod.Pix);
    }
}
