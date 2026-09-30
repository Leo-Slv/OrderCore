namespace OrderCore.Api.Modules.Payments.Infrastructure.Providers.Stripe;

/// <summary>
/// Stripe settings (section <c>Payments:Stripe</c>), from the environment or
/// user-secrets only — never committed. Stripe is the payment provider when
/// <see cref="SecretKey"/> is set; otherwise the fake is (spec decision 3).
/// </summary>
public sealed class StripeOptions
{
    public const string SectionName = "Payments:Stripe";

    /// <summary>Server-side key (<c>sk_test_…</c> in test mode). Never leaves the API.</summary>
    public string? SecretKey { get; set; }

    /// <summary>Browser-side key (<c>pk_test_…</c>) for the Payment Element; safe to hand out.</summary>
    public string? PublishableKey { get; set; }

    /// <summary>Signs the webhooks Stripe sends (<c>whsec_…</c>; the Stripe CLI prints one locally).</summary>
    public string? WebhookSecret { get; set; }

    /// <summary>Only to point at <c>stripe-mock</c> in tests; Stripe's own API otherwise.</summary>
    public string? ApiBase { get; set; }

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(20);

    public int MaxNetworkRetries { get; set; } = 2;

    /// <summary>
    /// A live secret key (<c>sk_live_…</c>, real money) is refused at startup
    /// unless this is true — a live key only on purpose (production-readiness
    /// spec, decision 5).
    /// </summary>
    public bool AllowLiveKeys { get; set; }

    public bool IsEnabled => !string.IsNullOrWhiteSpace(SecretKey);
}
