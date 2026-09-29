using FluentAssertions;
using OrderCore.Api.Modules.Payments.Application.DTOs;
using OrderCore.Api.Modules.Payments.Application.UseCases;
using OrderCore.Api.Modules.Payments.Domain.Enums;
using OrderCore.Api.Shared.Domain.Exceptions;
using Xunit;

namespace OrderCore.UnitTests.Payments;

/// <summary>Only the methods the configured provider takes are offered and accepted.</summary>
public sealed class PaymentMethodAvailabilityTests
{
    [Fact]
    public void The_available_methods_are_the_providers()
    {
        var provider = new StubPaymentProvider();
        provider.Methods.Remove(PaymentMethod.Pix);

        var available = new GetAvailablePaymentMethodsUseCase(provider).Execute();

        available.Provider.Should().Be("Stub");
        available.Methods.Should().Equal(PaymentMethod.Card);
        available.PublishableKey.Should().BeNull();
    }

    [Fact]
    public async Task A_method_the_provider_does_not_take_is_refused_before_any_payment_is_created()
    {
        var provider = new StubPaymentProvider();
        provider.Methods.Remove(PaymentMethod.Pix);
        var payments = new FakePaymentRepository();
        var useCase = new CreatePaymentUseCase(
            payments, provider, new FakePaymentsOutbox(), new FakeAuditLogService(), TestMetrics.Payments, TimeProvider.System);

        var act = () => useCase.ExecuteAsync(
            new CreatePaymentCommand(Guid.NewGuid(), 50m, "BRL", PaymentMethod.Pix, "idem-pix"), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainRuleViolationException>()).Which.Code.Should().Be("payment_method_unavailable");
        (await payments.GetByOrderIdAsync(Guid.NewGuid(), CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task The_payment_records_which_provider_processed_it()
    {
        var payments = new FakePaymentRepository();
        var orderId = Guid.NewGuid();
        var useCase = new CreatePaymentUseCase(
            payments, new StubPaymentProvider(), new FakePaymentsOutbox(), new FakeAuditLogService(), TestMetrics.Payments, TimeProvider.System);

        await useCase.ExecuteAsync(new CreatePaymentCommand(orderId, 50m, "BRL", PaymentMethod.Card, "idem-card"), CancellationToken.None);

        (await payments.GetByOrderIdAsync(orderId, CancellationToken.None))!.Provider.Should().Be("Stub");
    }
}
