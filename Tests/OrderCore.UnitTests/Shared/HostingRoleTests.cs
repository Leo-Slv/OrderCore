using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrderCore.Api.Shared.Infrastructure.Hosting;
using Xunit;

namespace OrderCore.UnitTests.Shared;

/// <summary><c>Hosting:Role</c> decides whether a copy of the API runs the background services.</summary>
public sealed class HostingRoleTests
{
    private static IConfiguration Config(string? role) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(role is null ? [] : new Dictionary<string, string?> { ["Hosting:Role"] = role })
            .Build();

    private sealed class FakeWorker : BackgroundService
    {
        protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
    }

    [Theory]
    [InlineData(null, HostingRole.All)]
    [InlineData("", HostingRole.All)]
    [InlineData("All", HostingRole.All)]
    [InlineData("web", HostingRole.Web)]
    [InlineData(" WORKER ", HostingRole.Worker)]
    public void The_role_is_read_case_insensitively_and_defaults_to_all(string? value, HostingRole expected)
    {
        HostingRoles.Read(Config(value)).Should().Be(expected);
    }

    [Fact]
    public void An_unknown_role_stops_the_api_naming_the_setting()
    {
        var read = () => HostingRoles.Read(Config("Frontend"));

        read.Should().Throw<InvalidOperationException>().WithMessage("Hosting:Role must be All, Web or Worker*'Frontend'*");
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("All", true)]
    [InlineData("Worker", true)]
    [InlineData("Web", false)]
    public void Only_a_web_copy_leaves_the_background_services_out(string? role, bool registered)
    {
        var services = new ServiceCollection().AddBackgroundWorker<FakeWorker>(Config(role));

        services.Any(descriptor => descriptor.ServiceType == typeof(IHostedService)).Should().Be(registered);
    }
}
