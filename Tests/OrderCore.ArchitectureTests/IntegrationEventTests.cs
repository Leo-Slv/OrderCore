using FluentAssertions;
using NetArchTest.Rules;
using OrderCore.Api.Shared.Application.Messaging;
using Xunit;

namespace OrderCore.ArchitectureTests;

/// <summary>
/// What crosses module boundaries over the broker
/// (Docs/specs/events/async-messaging.md): an integration event is a
/// public contract of the module that publishes it, kept in its
/// <c>Contracts.IntegrationEvents</c> namespace and free of the module's
/// internals, and a message handler may know another module only through
/// those contracts — so a module can later leave the process (Payments →
/// PayCore) without its consumers changing.
/// </summary>
public sealed class IntegrationEventTests
{
    private static readonly string[] Modules = { "Orders", "Customers", "Catalog", "Inventory", "Payments", "AuditLogs", "Identity", "Messaging" };

    private static readonly string[] Layers = { "Domain", "Application", "Infrastructure", "Presentation" };

    private static System.Reflection.Assembly ApiAssembly => typeof(Program).Assembly;

    [Fact]
    public void Integration_events_live_in_their_modules_contracts()
    {
        var result = Types.InAssembly(ApiAssembly)
            .That()
            .Inherit(typeof(IntegrationEvent))
            .And()
            .ResideInNamespace("OrderCore.Api.Modules")
            .Should()
            .ResideInNamespaceMatching(@"^OrderCore\.Api\.Modules\.\w+\.Contracts\.IntegrationEvents$")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FailureMessage(result));
    }

    [Fact]
    public void Contracts_do_not_depend_on_their_modules_internals()
    {
        foreach (var module in Modules)
        {
            var result = Types.InAssembly(ApiAssembly)
                .That()
                .ResideInNamespace($"OrderCore.Api.Modules.{module}.Contracts")
                .ShouldNot()
                .HaveDependencyOnAny(Layers.Select(layer => $"OrderCore.Api.Modules.{module}.{layer}").ToArray())
                .GetResult();

            result.IsSuccessful.Should().BeTrue(FailureMessage(result));
        }
    }

    [Fact]
    public void Message_handlers_know_other_modules_only_through_their_contracts()
    {
        var handlers = ApiAssembly.GetTypes()
            .Where(type => type.GetInterfaces().Any(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IIntegrationEventHandler<>)))
            .ToList();
        handlers.Should().NotBeEmpty();

        foreach (var module in Modules)
        {
            var otherModulesInternals = Modules
                .Where(other => other != module)
                .SelectMany(other => Layers.Select(layer => $"OrderCore.Api.Modules.{other}.{layer}"))
                .ToArray();
            var moduleHandlers = handlers
                .Where(h => h.Namespace!.StartsWith($"OrderCore.Api.Modules.{module}.", StringComparison.Ordinal))
                .Select(h => h.FullName!)
                .ToArray();
            if (moduleHandlers.Length == 0)
            {
                continue;
            }

            var result = Types.InAssembly(ApiAssembly)
                .That()
                .HaveNameMatching(string.Join('|', moduleHandlers.Select(name => $"^{name.Split('.').Last()}$")))
                .And()
                .ResideInNamespace($"OrderCore.Api.Modules.{module}")
                .ShouldNot()
                .HaveDependencyOnAny(otherModulesInternals)
                .GetResult();

            result.IsSuccessful.Should().BeTrue(FailureMessage(result));
        }
    }

    private static string FailureMessage(TestResult result) =>
        result.FailingTypes is null
            ? "Integration-event rule violated."
            : "Integration-event rule violated by: " + string.Join(", ", result.FailingTypeNames);
}
