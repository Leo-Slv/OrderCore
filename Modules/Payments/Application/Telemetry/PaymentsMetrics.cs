using System.Diagnostics.Metrics;

namespace OrderCore.Api.Modules.Payments.Application.Telemetry;

/// <summary>
/// Meter <c>OrderCore.Payments</c> (Docs/specs/observability):
/// authorizations (approved or declined, with the decline reason), captures,
/// voids, refunds, and how long each call to the payment provider took. Only
/// the BCL's <see cref="System.Diagnostics.Metrics"/>. The decline reason is
/// the provider's code (e.g. <c>card_declined</c>), never card data.
/// </summary>
public sealed class PaymentsMetrics
{
    public const string Name = "OrderCore.Payments";

    private readonly Counter<long> _authorizations;
    private readonly Counter<long> _captures;
    private readonly Counter<long> _voids;
    private readonly Counter<long> _refunds;
    private readonly Histogram<double> _providerDuration;

    public PaymentsMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(Name);
        _authorizations = meter.CreateCounter<long>(
            "ordercore.payments.authorizations", "{payment}", "Authorization outcomes, with the decline reason.");
        _captures = meter.CreateCounter<long>("ordercore.payments.captures", "{payment}", "Payments captured.");
        _voids = meter.CreateCounter<long>("ordercore.payments.voids", "{payment}", "Authorizations released without charging.");
        _refunds = meter.CreateCounter<long>("ordercore.payments.refunds", "{refund}", "Refunds, by outcome.");
        _providerDuration = meter.CreateHistogram<double>(
            "ordercore.payments.provider.duration", "s", "How long a call to the payment provider took, by operation and outcome.");
    }

    public void Authorized(string method) =>
        _authorizations.Add(1, new("ordercore.outcome", "approved"), new("ordercore.payment_method", method));

    public void Declined(string method, string? reason) =>
        _authorizations.Add(
            1, new("ordercore.outcome", "declined"), new("ordercore.payment_method", method), new("ordercore.decline_reason", reason ?? "unknown"));

    public void Captured() => _captures.Add(1);

    public void Voided() => _voids.Add(1);

    /// <param name="outcome"><c>completed</c> or <c>failed</c>.</param>
    public void Refunded(string outcome) => _refunds.Add(1, new KeyValuePair<string, object?>("ordercore.outcome", outcome));

    /// <param name="operation"><c>authorize</c>, <c>capture</c>, <c>void</c> or <c>refund</c>.</param>
    /// <param name="outcome"><c>succeeded</c>, <c>refused</c> or <c>error</c>.</param>
    public void ProviderCalled(string provider, string operation, string outcome, TimeSpan duration) =>
        _providerDuration.Record(
            duration.TotalSeconds,
            new("ordercore.payment_provider", provider),
            new("ordercore.operation", operation),
            new("ordercore.outcome", outcome));
}
