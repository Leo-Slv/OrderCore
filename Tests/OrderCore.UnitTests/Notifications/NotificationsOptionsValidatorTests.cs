using FluentAssertions;
using Microsoft.Extensions.Options;
using OrderCore.Api.Modules.Notifications.Infrastructure.Senders;
using Xunit;

namespace OrderCore.UnitTests.Notifications;

/// <summary>E-mail that could never go out stops the API at startup, naming the setting and never echoing the key.</summary>
public sealed class NotificationsOptionsValidatorTests
{
    private static IReadOnlyList<string> FailuresOf(string? from, string? resendKey = null, string? smtpHost = "localhost") =>
        new NotificationsOptionsValidator(
                Options.Create(new ResendOptions { ApiKey = resendKey }),
                Options.Create(new SmtpOptions { Host = smtpHost }))
            .Validate(null, new NotificationsOptions { From = from })
            .Failures?.ToList() ?? [];

    [Fact]
    public void The_local_defaults_are_accepted()
    {
        FailuresOf("OrderCore <no-reply@ordercore.local>").Should().BeEmpty();
    }

    [Fact]
    public void Resend_with_a_sender_is_accepted_without_an_smtp_host()
    {
        FailuresOf("no-reply@shop.example", resendKey: "re_123abc", smtpHost: null).Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not an address")]
    public void The_sender_must_be_an_address(string? from)
    {
        FailuresOf(from).Should().ContainSingle().Which.Should().StartWith("Notifications:From");
    }

    [Fact]
    public void A_key_that_isnt_resends_is_refused_without_being_echoed()
    {
        var failures = FailuresOf("no-reply@shop.example", resendKey: "sk_test_oops");

        failures.Should().ContainSingle().Which.Should().StartWith("Notifications:Resend:ApiKey").And.NotContain("sk_test_oops");
    }

    [Fact]
    public void Without_resend_an_smtp_host_is_required()
    {
        FailuresOf("no-reply@shop.example", smtpHost: " ").Should().ContainSingle().Which.Should().StartWith("Notifications:Smtp:Host");
    }
}
