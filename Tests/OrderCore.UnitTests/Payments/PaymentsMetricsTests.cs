using FluentAssertions;
using OrderCore.Api.Modules.Payments.Application.DTOs;
using OrderCore.Api.Modules.Payments.Application.Telemetry;
using OrderCore.Api.Modules.Payments.Application.UseCases;
using OrderCore.Api.Modules.Payments.Domain.Enums;
using OrderCore.Api.Modules.Payments.Infrastructure.Providers;
using Xunit;

namespace OrderCore.UnitTests.Payments;

/// <summary>Authorizations are counted by outcome and decline reason; every provider call is timed.</summary>
public sealed class PaymentsMetricsTests : IDisposable
{
    private readonly MetricsProbe _probe = new();
    private readonly PaymentsMetrics _metrics;

    public PaymentsMetricsTests()
    {
        _metrics = new PaymentsMetrics(_probe.Factory);
    }

    public void Dispose() => _probe.Dispose();

    private CreatePaymentUseCase CreatePayment(bool providerAccepts) => new(
        new FakePaymentRepository(),
        new MeasuredPaymentProvider(new StubPaymentProvider(providerAccepts), _metrics, "Stub"),
        new FakePaymentsOutbox(),
        new FakeAuditLogService(),
        _metrics,
        TimeProvider.System);

    private static CreatePaymentCommand Command() => new(Guid.NewGuid(), 100m, "BRL", PaymentMethod.Pix, $"idem-{Guid.NewGuid():N}");

    [Fact]
    public async Task An_approved_authorization_is_counted_with_its_method()
    {
        await CreatePayment(providerAccepts: true).ExecuteAsync(Command(), CancellationToken.None);

        var authorization = _probe.Of("ordercore.payments.authorizations").Should().ContainSingle().Subject;
        authorization.Tags.Should().Contain("ordercore.outcome", "approved").And.Contain("ordercore.payment_method", "Pix");
    }

    [Fact]
    public async Task A_declined_authorization_is_counted_with_the_decline_reason()
    {
        await CreatePayment(providerAccepts: false).ExecuteAsync(Command(), CancellationToken.None);

        _probe.Of("ordercore.payments.authorizations").Should().ContainSingle()
            .Which.Tags.Should().Contain("ordercore.outcome", "declined").And.Contain("ordercore.decline_reason", "stub_declined");
    }

    [Fact]
    public async Task Every_provider_call_is_timed_by_operation_and_outcome()
    {
        await CreatePayment(providerAccepts: false).ExecuteAsync(Command(), CancellationToken.None);

        _probe.Of("ordercore.payments.provider.duration").Should().ContainSingle()
            .Which.Tags.Should().Contain("ordercore.operation", "authorize")
            .And.Contain("ordercore.outcome", "refused")
            .And.Contain("ordercore.payment_provider", "Stub");
    }
}
