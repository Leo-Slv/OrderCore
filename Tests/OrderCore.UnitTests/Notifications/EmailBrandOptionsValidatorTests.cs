using FluentAssertions;
using OrderCore.Api.Modules.Notifications.Infrastructure.Templates;
using Xunit;

namespace OrderCore.UnitTests.Notifications;

/// <summary>A brand the e-mails can't render safely stops the API at startup, naming the setting.</summary>
public sealed class EmailBrandOptionsValidatorTests
{
    private static IReadOnlyList<string> FailuresOf(EmailBrandOptions options) =>
        new EmailBrandOptionsValidator().Validate(null, options).Failures?.ToList() ?? [];

    [Fact]
    public void The_defaults_are_accepted()
    {
        FailuresOf(new EmailBrandOptions()).Should().BeEmpty();
    }

    [Theory]
    [InlineData("red")]
    [InlineData("#fff")]
    [InlineData("#12345g")]
    [InlineData("#3b3fd9; background:url(x)")]
    [InlineData("")]
    public void The_colors_must_be_six_digit_hex(string color)
    {
        FailuresOf(new EmailBrandOptions { AccentColor = color }).Should().ContainSingle().Which.Should().StartWith("Notifications:Brand:AccentColor");
        FailuresOf(new EmailBrandOptions { AccentSoftColor = color }).Should().ContainSingle().Which.Should().StartWith("Notifications:Brand:AccentSoftColor");
    }

    [Fact]
    public void The_name_and_the_footer_are_required()
    {
        FailuresOf(new EmailBrandOptions { Name = " ", Footer = "" }).Should().HaveCount(2);
    }
}
