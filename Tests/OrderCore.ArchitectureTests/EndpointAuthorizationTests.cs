using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using OrderCore.Api.Shared.Presentation.Authentication;
using Xunit;

namespace OrderCore.ArchitectureTests;

/// <summary>
/// The API denies by default (a fallback policy requires a signed-in user),
/// but "who may call this" should be a decision made on purpose for every
/// endpoint, not inherited by accident. Every controller action must be
/// classified, on the action or its controller, with <c>[AllowAnonymous]</c>,
/// <c>[Authorize]</c> or <c>[Authorize(Policy = ...)]</c> using a known
/// policy.
/// </summary>
public sealed class EndpointAuthorizationTests
{
    private static readonly string[] KnownPolicies = [AuthorizationPolicies.Customer, AuthorizationPolicies.Admin];

    private static IEnumerable<MethodInfo> ControllerActions() =>
        typeof(Program).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(t => t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any());

    private static IEnumerable<object> AccessAttributes(MemberInfo member) =>
        member.GetCustomAttributes().Where(a => a is IAllowAnonymous or IAuthorizeData);

    [Fact]
    public void Every_controller_action_is_explicitly_classified()
    {
        var unclassified = ControllerActions()
            .Where(action => !AccessAttributes(action).Any() && !AccessAttributes(action.DeclaringType!).Any())
            .Select(action => $"{action.DeclaringType!.Name}.{action.Name}")
            .ToList();

        unclassified.Should().BeEmpty(
            "every action needs [AllowAnonymous], [Authorize] or [Authorize(Policy = ...)] on itself or its controller");
    }

    [Fact]
    public void Authorization_policies_used_by_controllers_exist()
    {
        var unknown = ControllerActions()
            .SelectMany(action => AccessAttributes(action).Concat(AccessAttributes(action.DeclaringType!)))
            .OfType<IAuthorizeData>()
            .Where(a => a.Policy is not null && !KnownPolicies.Contains(a.Policy))
            .Select(a => a.Policy)
            .Distinct()
            .ToList();

        unknown.Should().BeEmpty();
    }

    [Fact]
    public void Controllers_are_found()
    {
        ControllerActions().Should().HaveCountGreaterThan(20, "otherwise the rules above would pass vacuously");
    }
}
