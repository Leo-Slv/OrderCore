using FluentAssertions;
using OrderCore.Api.Shared.Presentation.Hosting;
using Xunit;

namespace OrderCore.UnitTests.Shared;

/// <summary>
/// Outside development, the settings a deployment must provide can't be
/// missing or still local (production-readiness spec, item 5).
/// </summary>
public sealed class ProductionSettingsValidatorTests
{
    private static IEnumerable<string> FailuresOf(ProductionSettings settings) =>
        new ProductionSettingsValidator().Validate(null, settings).Failures ?? [];

    private static ProductionSettings Valid() => new()
    {
        ConnectionString = "Host=db.internal;Database=ordercore;Username=app;Password=secret",
        AllowedOrigins = ["https://shop.example"],
        AllowedHosts = "api.shop.example",
        BrokerHost = "rabbitmq.internal",
    };

    [Fact]
    public void A_complete_production_configuration_is_accepted()
    {
        FailuresOf(Valid()).Should().BeEmpty();
    }

    [Fact]
    public void Every_missing_setting_is_named()
    {
        var failures = FailuresOf(new ProductionSettings { AllowedOrigins = [""] }).ToList();

        failures.Should().HaveCount(4);
        failures.Should().Contain(f => f.StartsWith("ConnectionStrings:OrderCoreDb"))
            .And.Contain(f => f.StartsWith("Cors:AllowedOrigins"))
            .And.Contain(f => f.StartsWith("AllowedHosts"))
            .And.Contain(f => f.StartsWith("RabbitMq:Host"));
    }

    [Fact]
    public void Development_values_are_refused()
    {
        var local = Valid();
        local.ConnectionString = "Host=localhost;Port=5433;Database=ordercore;Username=ordercore;Password=ordercore";
        local.AllowedOrigins = ["https://shop.example", "http://localhost:3000"];
        local.AllowedHosts = "*";

        var failures = FailuresOf(local).ToList();

        failures.Should().HaveCount(3);
        failures.Should().Contain(f => f.Contains("localhost (the development database)"))
            .And.Contain(f => f.Contains("localhost origin"))
            .And.Contain(f => f.Contains("not *"));
        failures.Should().NotContain(f => f.Contains("Password=ordercore"), "the connection string is never echoed");
    }
}
