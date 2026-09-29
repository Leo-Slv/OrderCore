using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using OrderCore.Api.Shared.Application.Observability;

namespace OrderCore.Api.Modules.Messaging.Infrastructure.Telemetry;

/// <summary>
/// Spans and metrics of the messaging pipeline (Docs/specs/observability,
/// stage 2), following the OpenTelemetry messaging conventions:
/// <list type="bullet">
/// <item>the relay publishes inside a <c>producer</c> span, child of the
/// trace that wrote the outbox row; the consumer host handles each delivery
/// inside a <c>consumer</c> span, child of that producer — so a checkout
/// and everything it causes in other modules is one trace, and each retry
/// is one more consumer span in it, tagged with its attempt;</item>
/// <item>meter <c>OrderCore.Messaging</c>: outbox backlog and the age of its
/// oldest row per module, messages published, publish failures, messages
/// consumed, retries, failed messages and handling duration.</item>
/// </list>
/// Only ids and names go into tags — never a payload.
/// </summary>
public sealed class MessagingTelemetry : IDisposable
{
    public const string Name = "OrderCore.Messaging";

    public const string AttemptTag = "ordercore.messaging.attempt";

    private static readonly ActivitySource Source = new(Name);

    private readonly Meter _meter;
    private readonly Counter<long> _published;
    private readonly Counter<long> _publishFailures;
    private readonly Counter<long> _consumed;
    private readonly Counter<long> _retries;
    private readonly Counter<long> _failed;
    private readonly Histogram<double> _handlingDuration;
    private readonly ConcurrentDictionary<string, (long Pending, double OldestAgeSeconds)> _backlog = new();

    public MessagingTelemetry(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(Name);
        _published = _meter.CreateCounter<long>(
            "ordercore.messaging.published", "{message}", "Messages the relay published and the broker confirmed.");
        _publishFailures = _meter.CreateCounter<long>(
            "ordercore.messaging.publish_failures", "{message}", "Publishes that failed and wait in the outbox for the next poll.");
        _consumed = _meter.CreateCounter<long>(
            "ordercore.messaging.consumed", "{message}", "Deliveries a consumer handled or recognised as duplicates.");
        _retries = _meter.CreateCounter<long>(
            "ordercore.messaging.retries", "{message}", "Deliveries sent to a waiting queue for another attempt.");
        _failed = _meter.CreateCounter<long>(
            "ordercore.messaging.failed", "{message}", "Messages set aside in failed_messages.");
        _handlingDuration = _meter.CreateHistogram(
            "ordercore.messaging.handling.duration", "s", "How long a consumer took to handle a delivery.", tags: null, advice: DurationBuckets.Seconds);
        _meter.CreateObservableGauge(
            "ordercore.messaging.outbox.pending",
            () => _backlog.Select(b => new Measurement<long>(b.Value.Pending, new KeyValuePair<string, object?>("ordercore.module", b.Key))),
            "{message}",
            "Outbox rows not yet published, per module.");
        _meter.CreateObservableGauge(
            "ordercore.messaging.outbox.oldest_age",
            () => _backlog.Select(b => new Measurement<double>(b.Value.OldestAgeSeconds, new KeyValuePair<string, object?>("ordercore.module", b.Key))),
            "s",
            "Age of the oldest unpublished outbox row, per module (0 when empty).");
    }

    /// <summary>
    /// The span a publish runs in: a child of the trace that wrote the row
    /// (or a new trace if it had none). <c>null</c> when nothing listens.
    /// </summary>
    public Activity? StartPublish(string exchange, string routingKey, Guid messageId, string? traceParent, string? traceState)
    {
        ActivityContext.TryParse(traceParent, traceState, out var parent);
        var activity = Source.StartActivity($"{exchange} send", ActivityKind.Producer, parent);
        activity?
            .SetTag("messaging.system", "rabbitmq")
            .SetTag("messaging.operation.type", "send")
            .SetTag("messaging.destination.name", exchange)
            .SetTag("messaging.rabbitmq.destination.routing_key", routingKey)
            .SetTag("messaging.message.id", messageId.ToString());
        return activity;
    }

    /// <summary>
    /// The span one delivery is handled in: a child of the message's
    /// <c>traceparent</c> (the producer). With no listener, a plain
    /// <see cref="Activity"/> keeps the trace context flowing anyway, so what
    /// the handler publishes still carries it.
    /// </summary>
    public Activity? StartProcess(string queue, string type, Guid messageId, int attempt, string? traceParent, string? traceState)
    {
        if (!ActivityContext.TryParse(traceParent, traceState, out var parent))
        {
            parent = default;
        }

        var activity = Source.StartActivity($"{queue} process", ActivityKind.Consumer, parent);
        if (activity is null && parent != default)
        {
            activity = new Activity($"{queue} process");
            activity.SetParentId(traceParent!);
            activity.TraceStateString = traceState;
            activity.Start();
        }

        activity?
            .SetTag("messaging.system", "rabbitmq")
            .SetTag("messaging.operation.type", "process")
            .SetTag("messaging.destination.name", queue)
            .SetTag("messaging.message.id", messageId.ToString())
            .SetTag("ordercore.messaging.type", type)
            .SetTag(AttemptTag, attempt);
        return activity;
    }

    public void Published(string module, string type) =>
        _published.Add(1, new("ordercore.module", module), new("ordercore.messaging.type", type));

    public void PublishFailed(string module, string type) =>
        _publishFailures.Add(1, new("ordercore.module", module), new("ordercore.messaging.type", type));

    /// <summary><paramref name="outcome"/>: <c>handled</c>, <c>duplicate</c> or <c>error</c>.</summary>
    public void Handled(string queue, string type, string outcome, TimeSpan duration)
    {
        var tags = new TagList
        {
            { "messaging.destination.name", queue },
            { "ordercore.messaging.type", type },
            { "ordercore.messaging.outcome", outcome },
        };
        if (outcome != "error")
        {
            _consumed.Add(1, tags);
        }

        _handlingDuration.Record(duration.TotalSeconds, tags);
    }

    /// <param name="nextAttempt">The attempt the message is scheduled for (2 to 5).</param>
    public void Retried(string queue, string type, int nextAttempt) =>
        _retries.Add(1, new("messaging.destination.name", queue), new("ordercore.messaging.type", type), new(AttemptTag, nextAttempt));

    /// <summary><paramref name="reason"/>: <c>retries_exhausted</c> or <c>unreadable</c>.</summary>
    public void SetAside(string queue, string type, string reason) =>
        _failed.Add(1, new("messaging.destination.name", queue), new("ordercore.messaging.type", type), new("ordercore.messaging.reason", reason));

    /// <summary>What the relay last measured, per module (the health details show it).</summary>
    public IReadOnlyDictionary<string, (long Pending, double OldestAgeSeconds)> Backlog => _backlog;

    /// <summary>Called by the relay on every poll, per outbox.</summary>
    public void ReportBacklog(string module, long pending, TimeSpan oldestAge) =>
        _backlog[module] = (pending, oldestAge.TotalSeconds);

    public void Dispose() => _meter.Dispose();
}
