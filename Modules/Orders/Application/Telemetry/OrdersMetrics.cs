using System.Diagnostics.Metrics;
using OrderCore.Api.Shared.Application.Observability;

namespace OrderCore.Api.Modules.Orders.Application.Telemetry;

/// <summary>
/// Meter <c>OrderCore.Orders</c> (Docs/specs/observability): orders by
/// lifecycle step, the value of confirmed orders per currency, and how
/// checkouts go — their duration by outcome and the refusals by error code.
/// Recorded by the use cases, after the change they count was saved. Only
/// the BCL's <see cref="System.Diagnostics.Metrics"/>: exporting is the
/// host's concern.
/// </summary>
public sealed class OrdersMetrics
{
    public const string Name = "OrderCore.Orders";

    private readonly Counter<long> _created;
    private readonly Counter<long> _confirmed;
    private readonly Counter<double> _confirmedValue;
    private readonly Counter<long> _paymentFailed;
    private readonly Counter<long> _unpaidExpired;
    private readonly Counter<long> _shipped;
    private readonly Counter<long> _delivered;
    private readonly Counter<long> _cancelled;
    private readonly Histogram<double> _checkoutDuration;
    private readonly Counter<long> _checkoutRefusals;

    public OrdersMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(Name);
        _created = meter.CreateCounter<long>("ordercore.orders.created", "{order}", "Orders placed.");
        _confirmed = meter.CreateCounter<long>("ordercore.orders.confirmed", "{order}", "Orders confirmed after their payment was authorized.");
        _confirmedValue = meter.CreateCounter<double>(
            "ordercore.orders.confirmed_value", "{currency_unit}", "Total value of confirmed orders, per currency.");
        _paymentFailed = meter.CreateCounter<long>("ordercore.orders.payment_failed", "{order}", "Orders whose payment was refused.");
        _unpaidExpired = meter.CreateCounter<long>(
            "ordercore.orders.unpaid_expired", "{order}", "Orders ended because no payment was started in time (their stock released).");
        _shipped = meter.CreateCounter<long>("ordercore.orders.shipped", "{order}", "Orders shipped.");
        _delivered = meter.CreateCounter<long>("ordercore.orders.delivered", "{order}", "Orders delivered.");
        _cancelled = meter.CreateCounter<long>("ordercore.orders.cancelled", "{order}", "Orders cancelled, by who cancelled them.");
        _checkoutDuration = meter.CreateHistogram(
            "ordercore.checkout.duration", "s", "How long a checkout took, by outcome.", tags: null, advice: DurationBuckets.Seconds);
        _checkoutRefusals = meter.CreateCounter<long>("ordercore.checkout.refusals", "{checkout}", "Checkouts refused, by error code.");
    }

    /// <param name="channel"><c>checkout</c> (the storefront) or <c>admin</c> (the step-by-step endpoints).</param>
    public void OrderCreated(string channel) => _created.Add(1, new KeyValuePair<string, object?>("ordercore.channel", channel));

    public void OrderConfirmed(decimal totalAmount, string currency)
    {
        var tag = new KeyValuePair<string, object?>("ordercore.currency", currency);
        _confirmed.Add(1, tag);
        _confirmedValue.Add((double)totalAmount, tag);
    }

    public void OrderPaymentFailed() => _paymentFailed.Add(1);

    public void UnpaidOrderExpired() => _unpaidExpired.Add(1);

    public void OrderShipped() => _shipped.Add(1);

    public void OrderDelivered() => _delivered.Add(1);

    /// <param name="cancelledBy"><c>customer</c>, <c>admin</c> or <c>system</c> (an expired authorization).</param>
    public void OrderCancelled(string cancelledBy) =>
        _cancelled.Add(1, new KeyValuePair<string, object?>("ordercore.cancelled_by", cancelledBy));

    /// <param name="outcome"><c>placed</c>, <c>repeated</c> (same idempotency key) or <c>refused</c>.</param>
    public void CheckoutFinished(string outcome, TimeSpan duration) =>
        _checkoutDuration.Record(duration.TotalSeconds, new KeyValuePair<string, object?>("ordercore.outcome", outcome));

    /// <param name="code">The error code the customer got, e.g. <c>insufficient_stock</c>.</param>
    public void CheckoutRefused(string code) =>
        _checkoutRefusals.Add(1, new KeyValuePair<string, object?>("ordercore.error_code", code));
}
