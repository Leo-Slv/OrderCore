using FluentAssertions;
using OrderCore.Api.Modules.Payments.Infrastructure.Providers.Stripe;
using Xunit;

namespace OrderCore.UnitTests.Payments;

/// <summary>
/// The Stripe settings that would make the API run but misbehave are refused
/// at startup (production-readiness spec, decision 5).
/// </summary>
public sealed class StripeOptionsValidatorTests
{
    private static IEnumerable<string> FailuresOf(StripeOptions options) =>
        new StripeOptionsValidator().Validate(null, options).Failures ?? [];

    private static StripeOptions Test() => new()
    {
        SecretKey = "sk_test_abc",
        PublishableKey = "pk_test_abc",
        WebhookSecret = "whsec_abc",
    };

    [Fact]
    public void Without_a_secret_key_the_fake_provider_is_used_and_nothing_is_checked()
    {
        FailuresOf(new StripeOptions { PublishableKey = "pk_live_abc" }).Should().BeEmpty();
    }

    [Fact]
    public void A_complete_test_configuration_is_accepted()
    {
        FailuresOf(Test()).Should().BeEmpty();
    }

    [Fact]
    public void A_live_key_needs_explicit_permission()
    {
        var live = Test();
        live.SecretKey = "sk_live_abc";
        live.PublishableKey = "pk_live_abc";

        FailuresOf(live).Should().ContainSingle().Which.Should().Contain("AllowLiveKeys");

        live.AllowLiveKeys = true;
        FailuresOf(live).Should().BeEmpty();
    }

    [Fact]
    public void Keys_of_different_modes_are_refused()
    {
        var mixed = Test();
        mixed.PublishableKey = "pk_live_abc";

        FailuresOf(mixed).Should().ContainSingle().Which.Should().Contain("same mode");
    }

    [Fact]
    public void The_publishable_key_and_the_webhook_secret_are_required()
    {
        var incomplete = Test();
        incomplete.PublishableKey = null;
        incomplete.WebhookSecret = " ";

        var failures = FailuresOf(incomplete).ToList();

        failures.Should().HaveCount(2);
        failures.Should().Contain(f => f.Contains("PublishableKey")).And.Contain(f => f.Contains("WebhookSecret"));
    }

    [Fact]
    public void Something_that_is_not_a_secret_key_is_refused_without_echoing_it()
    {
        var wrong = Test();
        wrong.SecretKey = "pk_test_pasted_in_the_wrong_place";

        FailuresOf(wrong).Should().Contain(f => f.Contains("must be a Stripe secret key"))
            .And.NotContain(f => f.Contains("pasted_in_the_wrong_place"));
    }
}
