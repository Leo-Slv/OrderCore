using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using OrderCore.Api.Modules.Inventory.Application.Telemetry;
using OrderCore.Api.Modules.Orders.Application.Telemetry;
using OrderCore.Api.Modules.Payments.Application.Telemetry;

namespace OrderCore.IntegrationTests;

/// <summary>The modules' metrics, for use cases built by hand; nothing listens unless a test does.</summary>
public static class TestMetrics
{
    private static readonly IMeterFactory Factory =
        new ServiceCollection().AddMetrics().BuildServiceProvider().GetRequiredService<IMeterFactory>();

    public static OrdersMetrics Orders { get; } = new(Factory);

    public static PaymentsMetrics Payments { get; } = new(Factory);

    public static InventoryMetrics Inventory { get; } = new(Factory);
}
