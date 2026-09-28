using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;

namespace OrderCore.UnitTests;

/// <summary>
/// A meter factory of the test's own and everything recorded on it, so a
/// test sees only its own measurements.
/// </summary>
public sealed class MetricsProbe : IDisposable
{
    private readonly ServiceProvider _services = new ServiceCollection().AddMetrics().BuildServiceProvider();
    private readonly MeterListener _listener = new();
    private readonly List<(string Instrument, double Value, Dictionary<string, object?> Tags)> _measurements = [];

    public MetricsProbe()
    {
        Factory = _services.GetRequiredService<IMeterFactory>();
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Scope == Factory)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Record(instrument, value, tags));
        _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Record(instrument, value, tags));
        _listener.Start();
    }

    public IMeterFactory Factory { get; }

    /// <summary>The measurements of one instrument, in the order they were recorded.</summary>
    public IReadOnlyList<(double Value, Dictionary<string, object?> Tags)> Of(string instrument)
    {
        lock (_measurements)
        {
            return _measurements.Where(m => m.Instrument == instrument).Select(m => (m.Value, m.Tags)).ToList();
        }
    }

    public void Dispose()
    {
        _listener.Dispose();
        _services.Dispose();
    }

    private void Record(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var copy = new Dictionary<string, object?>();
        foreach (var tag in tags)
        {
            copy[tag.Key] = tag.Value;
        }

        lock (_measurements)
        {
            _measurements.Add((instrument.Name, value, copy));
        }
    }
}
