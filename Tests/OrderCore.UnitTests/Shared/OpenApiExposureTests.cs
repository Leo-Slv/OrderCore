using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using OrderCore.Api.Shared.Presentation.OpenApi;
using Xunit;

namespace OrderCore.UnitTests.Shared;

/// <summary>The OpenAPI document and Scalar are Development-only unless <c>OpenApi:Expose</c> says otherwise.</summary>
public sealed class OpenApiExposureTests
{
    private static bool Enabled(string environment, string? expose)
    {
        var host = new FakeHost(environment);
        var settings = new Dictionary<string, string?>();
        if (expose is not null)
        {
            settings["OpenApi:Expose"] = expose;
        }

        return OpenApiExposure.IsEnabled(host, new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
    }

    private sealed class FakeHost(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "OrderCore.Api";

        public string ContentRootPath { get; set; } = ".";

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    [Fact]
    public void Development_always_exposes_it() => Enabled("Development", null).Should().BeTrue();

    [Fact]
    public void Production_does_not_by_default() => Enabled("Production", null).Should().BeFalse();

    [Fact]
    public void Production_exposes_it_when_the_setting_is_true() => Enabled("Production", "true").Should().BeTrue();

    [Fact]
    public void A_false_setting_keeps_production_closed() => Enabled("Production", "false").Should().BeFalse();
}
