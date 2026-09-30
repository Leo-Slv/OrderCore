using System.Globalization;
using Microsoft.Extensions.Options;
using OrderCore.Api.Modules.Payments.Domain.Entities;
using OrderCore.Api.Modules.Payments.Domain.Enums;
using OrderCore.Api.Modules.Payments.Domain.Repositories;
using Stripe;
using PaymentMethod = OrderCore.Api.Modules.Payments.Domain.Enums.PaymentMethod;

namespace OrderCore.Api.Modules.Payments.Infrastructure.Providers.Stripe;

/// <summary>
/// Stripe through its official SDK (Docs/specs/payments/stripe-provider.md):
/// <list type="bullet">
/// <item>authorizing creates a PaymentIntent with <c>capture_method=manual</c>,
/// card only, the amount in the currency's smallest unit and the order and
/// payment ids in its metadata — and answers "waiting for the buyer" with its
/// id and client secret: the card is confirmed in the browser (Payment
/// Element, 3-D Secure in place) and the outcome arrives by webhook;</item>
/// <item>capture, void (cancelling the intent) and refund act on that intent;</item>
/// <item>every call that creates or moves money sends an idempotency key
/// derived from the payment (or the refund), so a retried call never charges
/// twice; the SDK retries network failures, each call has a timeout;</item>
/// <item>a card error comes back as a refusal with Stripe's decline code;
/// anything else (network, Stripe down, bad configuration) is thrown, as the
/// fake's <c>Timeout</c>/<c>Unavailable</c> modes do.</item>
/// </list>
/// No card data ever passes through OrderCore, and the client secret isn't stored.
/// </summary>
public sealed class StripePaymentProvider : IPaymentProvider
{
    /// <summary>Currencies Stripe counts in whole units (no cents).</summary>
    private static readonly HashSet<string> ZeroDecimalCurrencies =
        new(StringComparer.OrdinalIgnoreCase) { "BIF", "CLP", "DJF", "GNF", "JPY", "KMF", "KRW", "MGA", "PYG", "RWF", "UGX", "VND", "VUV", "XAF", "XOF", "XPF" };

    private readonly PaymentIntentService _paymentIntents;
    private readonly RefundService _refunds;

    /// <summary>The named <see cref="HttpClient"/> Stripe calls go through (registered in PaymentsDependencyInjection).</summary>
    public const string HttpClientName = "stripe";

    public StripePaymentProvider(IOptions<StripeOptions> options, IHttpClientFactory httpClients)
    {
        var settings = options.Value;
        var http = httpClients.CreateClient(HttpClientName);
        http.Timeout = settings.RequestTimeout;
        var httpClient = new SystemNetHttpClient(http, maxNetworkRetries: settings.MaxNetworkRetries);
        var client = new StripeClient(
            apiKey: settings.SecretKey,
            httpClient: httpClient,
            apiBase: string.IsNullOrWhiteSpace(settings.ApiBase) ? null : settings.ApiBase);

        _paymentIntents = new PaymentIntentService(client);
        _refunds = new RefundService(client);
        Info = new PaymentProviderInfo("Stripe", [PaymentMethod.Card], settings.PublishableKey);
    }

    public PaymentProviderInfo Info { get; }

    public async Task<PaymentAuthorizationResult> AuthorizeAsync(Payment payment, CancellationToken cancellationToken)
    {
        try
        {
            var intent = await _paymentIntents.CreateAsync(
                new PaymentIntentCreateOptions
                {
                    Amount = ToMinorUnits(payment.Amount, payment.Currency),
                    Currency = payment.Currency.ToLowerInvariant(),
                    CaptureMethod = "manual",
                    PaymentMethodTypes = ["card"],
                    Metadata = new Dictionary<string, string>
                    {
                        ["order_id"] = payment.OrderId.ToString(),
                        ["payment_id"] = payment.Id.ToString(),
                    },
                },
                Idempotency(payment, "authorize"),
                cancellationToken);

            return PaymentAuthorizationResult.WaitingForBuyer(intent.Id, intent.ClientSecret);
        }
        catch (StripeException exception) when (IsRefusal(exception))
        {
            return new PaymentAuthorizationResult(Succeeded: false, ProviderReference: null, FailureReason: ReasonOf(exception));
        }
    }

    public async Task<PaymentCaptureResult> CaptureAsync(Payment payment, CancellationToken cancellationToken)
    {
        try
        {
            await _paymentIntents.CaptureAsync(IntentOf(payment), options: null, Idempotency(payment, "capture"), cancellationToken);
            return new PaymentCaptureResult(Succeeded: true, FailureReason: null);
        }
        catch (StripeException exception) when (IsRefusal(exception))
        {
            return new PaymentCaptureResult(Succeeded: false, FailureReason: ReasonOf(exception));
        }
    }

    public async Task<PaymentVoidResult> VoidAsync(Payment payment, CancellationToken cancellationToken)
    {
        try
        {
            await _paymentIntents.CancelAsync(IntentOf(payment), options: null, Idempotency(payment, "cancel"), cancellationToken);
            return new PaymentVoidResult(Succeeded: true, FailureReason: null);
        }
        catch (StripeException exception) when (IsRefusal(exception))
        {
            return new PaymentVoidResult(Succeeded: false, FailureReason: ReasonOf(exception));
        }
    }

    /// <summary>
    /// Retrieves the intent: while it still needs the buyer (no card yet, a
    /// declined card to retry, 3-D Secure pending), its client secret.
    /// </summary>
    public async Task<string?> GetClientSecretAsync(Payment payment, CancellationToken cancellationToken)
    {
        var intent = await _paymentIntents.GetAsync(IntentOf(payment), options: null, requestOptions: null, cancellationToken);
        return intent.Status is "requires_payment_method" or "requires_confirmation" or "requires_action"
            ? intent.ClientSecret
            : null;
    }

    /// <summary>The intent as Stripe has it, in OrderCore's terms.</summary>
    public async Task<PaymentProviderState> GetStateAsync(Payment payment, CancellationToken cancellationToken)
    {
        var intent = await _paymentIntents.GetAsync(
            IntentOf(payment), new PaymentIntentGetOptions { Expand = ["latest_charge"] }, requestOptions: null, cancellationToken);

        return intent.Status switch
        {
            "requires_capture" => new PaymentProviderState(PaymentProviderStatus.Authorized, AuthorizationExpiresAt: CaptureDeadlineOf(intent)),
            "succeeded" => new PaymentProviderState(PaymentProviderStatus.Captured),
            "canceled" => new PaymentProviderState(PaymentProviderStatus.Canceled, AuthorizationExpired: intent.CancellationReason == "automatic"),
            _ => new PaymentProviderState(
                PaymentProviderStatus.WaitingForBuyer, intent.LastPaymentError?.DeclineCode ?? intent.LastPaymentError?.Code),
        };
    }

    /// <summary>
    /// Until when an authorized intent can be captured: its card charge's
    /// <c>capture_before</c> (it varies by card network and country); null
    /// when Stripe doesn't say.
    /// </summary>
    public async Task<DateTimeOffset?> GetCaptureDeadlineAsync(string paymentIntentId, CancellationToken cancellationToken)
    {
        var intent = await _paymentIntents.GetAsync(
            paymentIntentId, new PaymentIntentGetOptions { Expand = ["latest_charge"] }, requestOptions: null, cancellationToken);
        return CaptureDeadlineOf(intent);
    }

    private static DateTimeOffset? CaptureDeadlineOf(PaymentIntent intent) =>
        intent.LatestCharge?.PaymentMethodDetails?.Card?.CaptureBefore is { } captureBefore
            ? new DateTimeOffset(DateTime.SpecifyKind(captureBefore, DateTimeKind.Utc))
            : null;

    /// <summary>Refunds the payment's pending refund — the one the use case just requested.</summary>
    public async Task<PaymentRefundResult> RefundAsync(Payment payment, CancellationToken cancellationToken)
    {
        var refund = payment.Refunds.LastOrDefault(r => r.Status == RefundStatus.Pending)
            ?? throw new InvalidOperationException($"Payment '{payment.Id}' has no pending refund to send to Stripe.");

        try
        {
            await _refunds.CreateAsync(
                new RefundCreateOptions
                {
                    PaymentIntent = IntentOf(payment),
                    Amount = ToMinorUnits(refund.Amount, payment.Currency),
                    Metadata = new Dictionary<string, string> { ["refund_id"] = refund.Id.ToString() },
                },
                new RequestOptions { IdempotencyKey = $"ordercore-refund-{refund.Id:N}" },
                cancellationToken);
            return new PaymentRefundResult(Succeeded: true, FailureReason: null);
        }
        catch (StripeException exception) when (IsRefusal(exception))
        {
            return new PaymentRefundResult(Succeeded: false, FailureReason: ReasonOf(exception));
        }
    }

    /// <summary>The amount as Stripe counts it: cents for BRL/USD, whole units for JPY and the like.</summary>
    public static long ToMinorUnits(decimal amount, string currency) =>
        ZeroDecimalCurrencies.Contains(currency)
            ? decimal.ToInt64(decimal.Round(amount, 0, MidpointRounding.AwayFromZero))
            : decimal.ToInt64(decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));

    /// <summary>One key per payment and operation: repeating the call can't repeat its effect at Stripe.</summary>
    public static RequestOptions Idempotency(Payment payment, string operation) =>
        new() { IdempotencyKey = string.Create(CultureInfo.InvariantCulture, $"ordercore-{payment.Id:N}-{operation}") };

    private static string IntentOf(Payment payment) =>
        payment.ProviderReference
            ?? throw new InvalidOperationException($"Payment '{payment.Id}' has no Stripe PaymentIntent.");

    /// <summary>
    /// Stripe said no to this request (a card was declined, the intent is in
    /// the wrong state, the amount is invalid) — as opposed to Stripe being
    /// unreachable or misconfigured, which is thrown.
    /// </summary>
    private static bool IsRefusal(StripeException exception) =>
        exception.StripeError?.Type is "card_error" or "invalid_request_error";

    private static string ReasonOf(StripeException exception) =>
        exception.StripeError?.DeclineCode ?? exception.StripeError?.Code ?? "stripe_refused";
}
