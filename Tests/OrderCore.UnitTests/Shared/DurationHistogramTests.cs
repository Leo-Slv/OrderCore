using System.Diagnostics.Metrics;
using FluentAssertions;
using OrderCore.Api.Modules.Inventory.Application.Telemetry;
using OrderCore.Api.Modules.Messaging.Infrastructure.Telemetry;
using OrderCore.Api.Modules.Orders.Application.Telemetry;
using OrderCore.Api.Modules.Payments.Application.Telemetry;
using OrderCore.Api.Shared.Application.Observability;
using Xunit;

namespace OrderCore.UnitTests.Shared;

/// <summary>
/// Every duration histogram recorded in seconds declares second-sized
/// buckets; with the SDK's default (millisecond-sized) ones, every
/// percentile of a sub-second duration came out as the same 4.75 s.
/// </summary>
public sealed class DurationHistogramTests
{
    [Fact]
    public void Every_histogram_in_seconds_declares_second_sized_buckets()
    {
        using var probe = new MetricsProbe();
        var histograms = new List<Instrument>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, _) =>
            {
                if (instrument.Meter.Scope == probe.Factory && instrument.Unit == "s")
                {
                    histograms.Add(instrument);
                }
            },
        };
        listener.Start();

        _ = new OrdersMetrics(probe.Factory);
        _ = new PaymentsMetrics(probe.Factory);
        _ = new InventoryMetrics(probe.Factory);
        using var messaging = new MessagingTelemetry(probe.Factory);

        histograms.OfType<Histogram<double>>().Select(h => h.Name).Should().BeEquivalentTo(
            ["ordercore.checkout.duration", "ordercore.payments.provider.duration", "ordercore.messaging.handling.duration"]);
        histograms.OfType<Histogram<double>>().Should().OnlyContain(
            h => h.Advice != null && h.Advice.HistogramBucketBoundaries!.SequenceEqual(DurationBuckets.Seconds.HistogramBucketBoundaries!));
    }
}
