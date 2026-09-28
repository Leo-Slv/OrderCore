using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OrderCore.Api.Shared.Application.Observability;

namespace OrderCore.Api.Shared.Infrastructure.Observability;

/// <summary>
/// Copies the ids a use case tagged on its span (<see cref="Observed"/>) —
/// order, payment, customer, product — onto every log line written inside
/// that span or below it, so all the logs of one order can be found by its
/// id without every use case opening a log scope. A tag the log line already
/// has is left as it is.
/// </summary>
public sealed class SpanIdsLogProcessor : BaseProcessor<LogRecord>
{
    public override void OnEnd(LogRecord data)
    {
        List<KeyValuePair<string, object?>>? ids = null;
        for (var activity = Activity.Current; activity is not null; activity = activity.Parent)
        {
            foreach (var key in Observed.Ids)
            {
                if (activity.GetTagItem(key) is { } value
                    && ids?.Exists(i => i.Key == key) != true
                    && data.Attributes?.Any(a => a.Key == key) != true)
                {
                    (ids ??= []).Add(new(key, value));
                }
            }
        }

        if (ids is not null)
        {
            data.Attributes = [.. data.Attributes ?? [], .. ids];
        }
    }
}
