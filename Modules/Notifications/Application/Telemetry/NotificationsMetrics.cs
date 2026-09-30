using System.Diagnostics.Metrics;
using OrderCore.Api.Shared.Application.Observability;

namespace OrderCore.Api.Modules.Notifications.Application.Telemetry;

/// <summary>
/// Meter <c>OrderCore.Notifications</c> (Docs/specs/observability): e-mails
/// by template and outcome, and how long each call to the e-mail provider
/// took. Never an address, a subject or a link.
/// </summary>
public sealed class NotificationsMetrics
{
    public const string Name = "OrderCore.Notifications";

    private readonly Counter<long> _emails;
    private readonly Histogram<double> _sendDuration;

    public NotificationsMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(Name);
        _emails = meter.CreateCounter<long>(
            "ordercore.notifications.emails", "{email}", "E-mails by template and outcome: queued, sent, retried or failed.");
        _sendDuration = meter.CreateHistogram(
            "ordercore.notifications.send.duration",
            "s",
            "How long a call to the e-mail provider took, by provider and outcome.",
            tags: null,
            advice: DurationBuckets.Seconds);
    }

    /// <param name="outcome"><c>queued</c>, <c>sent</c>, <c>retried</c> (a failed attempt, due again) or <c>failed</c> (given up on).</param>
    public void Email(string template, string outcome) =>
        _emails.Add(1, new("ordercore.email_template", template), new("ordercore.outcome", outcome));

    /// <param name="outcome"><c>sent</c>, <c>rejected</c> or <c>error</c>.</param>
    public void ProviderCalled(string provider, string outcome, TimeSpan duration) =>
        _sendDuration.Record(duration.TotalSeconds, new("ordercore.email_provider", provider), new("ordercore.outcome", outcome));
}
