using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace OrderCore.ArchitectureTests;

/// <summary>
/// Validates the structural rules from section 8 and section 33 of the
/// project context. These are the rules that make the eventual Payments
/// -&gt; PayCore extraction (section 22) safe: if Domain ever starts
/// depending on Infrastructure, or one module starts reaching into
/// another module's internals, this suite is where that regression is
/// caught — not by convention or code review alone.
///
/// OrderCore is a single project (mirroring CourseCore's layout — section
/// 5), so these rules are validated purely at the namespace level via
/// NetArchTest, not by project-reference boundaries between assemblies.
/// </summary>
public sealed class ModuleBoundaryTests
{
    private static readonly string[] Modules = { "Orders", "Customers", "Catalog", "Inventory", "Payments" };

    private static System.Reflection.Assembly ApiAssembly => typeof(Program).Assembly;

    [Fact]
    public void Domain_does_not_depend_on_Infrastructure()
    {
        foreach (var module in Modules)
        {
            var result = Types.InAssembly(ApiAssembly)
                .That()
                .ResideInNamespace($"OrderCore.Api.Modules.{module}.Domain")
                .ShouldNot()
                .HaveDependencyOn($"OrderCore.Api.Modules.{module}.Infrastructure")
                .GetResult();

            result.IsSuccessful.Should().BeTrue(BuildFailureMessage(module, result));
        }
    }

    [Fact]
    public void Domain_does_not_depend_on_Presentation()
    {
        foreach (var module in Modules)
        {
            var result = Types.InAssembly(ApiAssembly)
                .That()
                .ResideInNamespace($"OrderCore.Api.Modules.{module}.Domain")
                .ShouldNot()
                .HaveDependencyOn($"OrderCore.Api.Modules.{module}.Presentation")
                .GetResult();

            result.IsSuccessful.Should().BeTrue(BuildFailureMessage(module, result));
        }
    }

    [Fact]
    public void Domain_does_not_depend_on_aspnetcore_or_efcore()
    {
        foreach (var module in Modules)
        {
            var result = Types.InAssembly(ApiAssembly)
                .That()
                .ResideInNamespace($"OrderCore.Api.Modules.{module}.Domain")
                .ShouldNot()
                .HaveDependencyOnAny("Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore")
                .GetResult();

            result.IsSuccessful.Should().BeTrue(BuildFailureMessage(module, result));
        }
    }

    [Fact]
    public void Application_does_not_depend_on_Infrastructure()
    {
        foreach (var module in Modules)
        {
            var result = Types.InAssembly(ApiAssembly)
                .That()
                .ResideInNamespace($"OrderCore.Api.Modules.{module}.Application")
                .ShouldNot()
                .HaveDependencyOn($"OrderCore.Api.Modules.{module}.Infrastructure")
                .GetResult();

            result.IsSuccessful.Should().BeTrue(BuildFailureMessage(module, result));
        }
    }

    [Fact]
    public void Orders_domain_does_not_depend_on_another_modules_domain_or_infrastructure()
    {
        // Orders is allowed to depend on Application-level contracts of
        // other modules (e.g. IProductCatalog, which itself lives under
        // Modules.Orders.Application.Contracts) but the Order aggregate
        // itself never reaches directly into another module's Domain or
        // Infrastructure namespace (section 7 — the "Application Contract"
        // indirection).
        var result = Types.InAssembly(ApiAssembly)
            .That()
            .ResideInNamespace("OrderCore.Api.Modules.Orders.Domain")
            .ShouldNot()
            .HaveDependencyOnAny(
                "OrderCore.Api.Modules.Payments.Domain",
                "OrderCore.Api.Modules.Payments.Infrastructure",
                "OrderCore.Api.Modules.Inventory.Domain",
                "OrderCore.Api.Modules.Inventory.Infrastructure",
                "OrderCore.Api.Modules.Customers.Domain",
                "OrderCore.Api.Modules.Customers.Infrastructure",
                "OrderCore.Api.Modules.Catalog.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildFailureMessage("Orders", result));
    }

    private static string BuildFailureMessage(string module, TestResult result) =>
        result.FailingTypes is null
            ? $"Architecture rule violated in module '{module}'."
            : $"Architecture rule violated in module '{module}' by: " + string.Join(", ", result.FailingTypeNames);
}
