using System.Diagnostics;

namespace OrderCore.Api.Shared.Application.Observability;

/// <summary>
/// Tags the current span (the request, or the message being handled) with
/// the ids a use case works on, so a trace can be found by order, payment,
/// customer or product — and, because the logging pipeline copies these
/// tags onto every log line written inside the span, so can its logs.
/// <para>
/// Only ids: never an e-mail, a name, an address, a document, a secret or
/// payment data (Docs/specs/observability/observability.md). Uses only the
/// BCL's <see cref="Activity"/>, so the Application layer stays free of
/// OpenTelemetry.
/// </para>
/// </summary>
public static class Observed
{
    public const string OrderId = "order.id";
    public const string PaymentId = "payment.id";
    public const string CustomerId = "customer.id";
    public const string ProductId = "product.id";

    /// <summary>Every tag that is copied onto log lines.</summary>
    public static readonly IReadOnlyList<string> Ids = [OrderId, PaymentId, CustomerId, ProductId];

    public static void Order(Guid orderId) => Tag(OrderId, orderId);

    public static void Payment(Guid paymentId) => Tag(PaymentId, paymentId);

    public static void Customer(Guid? customerId)
    {
        if (customerId is { } id)
        {
            Tag(CustomerId, id);
        }
    }

    public static void Product(Guid productId) => Tag(ProductId, productId);

    private static void Tag(string key, Guid id) => Activity.Current?.SetTag(key, id.ToString());
}
