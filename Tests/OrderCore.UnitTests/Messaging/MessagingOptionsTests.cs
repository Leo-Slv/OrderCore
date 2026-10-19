using FluentAssertions;
using Microsoft.Extensions.Configuration;
using OrderCore.Api.Modules.Messaging.Infrastructure.RabbitMq;
using Xunit;

namespace OrderCore.UnitTests.Messaging;

/// <summary>The relay's poll interval is the one messaging setting a deployment may change.</summary>
public sealed class MessagingOptionsTests
{
    private static MessagingOptions Applied(string? interval)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(interval is null ? [] : new Dictionary<string, string?> { ["Messaging:RelayPollInterval"] = interval })
            .Build();
        var options = new MessagingOptions();
        options.ApplyConfiguration(configuration);
        return options;
    }

    [Fact]
    public void The_default_stays_one_second()
    {
        Applied(null).RelayPollInterval.Should().Be(TimeSpan.FromSeconds(1));
        Applied("").RelayPollInterval.Should().Be(TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData("00:00:10", 10)]
    [InlineData("00:01:00", 60)]
    [InlineData("00:00:00.500", 0.5)]
    public void A_configured_interval_replaces_the_default(string value, double seconds)
    {
        Applied(value).RelayPollInterval.Should().Be(TimeSpan.FromSeconds(seconds));
    }

    [Theory]
    [InlineData("soon")]
    [InlineData("00:00:00")]
    [InlineData("-00:00:05")]
    [InlineData("00:00:00.050")]
    [InlineData("01:00:00")]
    public void An_unusable_interval_stops_the_api_naming_the_setting(string value)
    {
        var apply = () => Applied(value);

        apply.Should().Throw<InvalidOperationException>().WithMessage("Messaging:RelayPollInterval must be a duration*");
    }

    [Fact]
    public void The_retry_schedule_is_not_configurable()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Messaging:RetryDelays:0"] = "00:00:01" })
            .Build();
        var options = new MessagingOptions();

        options.ApplyConfiguration(configuration);

        options.RetryDelays.Should().HaveCount(4).And.StartWith(TimeSpan.FromSeconds(10));
    }
}
