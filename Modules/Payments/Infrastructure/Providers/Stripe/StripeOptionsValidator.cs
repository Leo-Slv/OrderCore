using Microsoft.Extensions.Options;

namespace OrderCore.Api.Modules.Payments.Infrastructure.Providers.Stripe;

/// <summary>
/// Stops the API at startup when the Stripe settings would make it run but
/// misbehave (production-readiness spec, item 5 and decision 5) — only when a
/// secret key is set, since without one the fake provider is used:
/// <list type="bullet">
/// <item>a live secret key without <c>Payments:Stripe:AllowLiveKeys=true</c>;</item>
/// <item>no publishable key, or one of the other mode (the browser would
/// confirm cards against a different Stripe environment);</item>
/// <item>no webhook secret (payments would only be confirmed by
/// reconciliation, minutes later).</item>
/// </list>
/// Each failure names the setting to fix. Keys are never echoed.
/// </summary>
public sealed class StripeOptionsValidator : IValidateOptions<StripeOptions>
{
    public ValidateOptionsResult Validate(string? name, StripeOptions options)
    {
        if (!options.IsEnabled)
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();
        var secretMode = ModeOf(options.SecretKey!, "sk_");
        if (secretMode is null)
        {
            failures.Add("Payments:Stripe:SecretKey must be a Stripe secret key (sk_test_… or sk_live_…).");
        }
        else if (secretMode == "live" && !options.AllowLiveKeys)
        {
            failures.Add("Payments:Stripe:SecretKey is a live key; set Payments:Stripe:AllowLiveKeys=true to use real money on purpose.");
        }

        if (string.IsNullOrWhiteSpace(options.PublishableKey))
        {
            failures.Add("Payments:Stripe:PublishableKey is required with a secret key (the storefront's card form needs it).");
        }
        else if (ModeOf(options.PublishableKey, "pk_") is var publishableMode && publishableMode != secretMode)
        {
            failures.Add("Payments:Stripe:PublishableKey must be of the same mode (test or live) as the secret key.");
        }

        if (string.IsNullOrWhiteSpace(options.WebhookSecret))
        {
            failures.Add("Payments:Stripe:WebhookSecret is required with a secret key; without it payments are only confirmed by reconciliation.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    /// <summary><c>test</c> or <c>live</c> for a key with <paramref name="prefix"/>; null for anything else.</summary>
    private static string? ModeOf(string key, string prefix) =>
        key.StartsWith(prefix + "test_", StringComparison.Ordinal) ? "test"
        : key.StartsWith(prefix + "live_", StringComparison.Ordinal) ? "live"
        : null;
}
