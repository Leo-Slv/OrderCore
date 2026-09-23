using OrderCore.Api.Shared.Application.Abstractions;
using OrderCore.Api.Shared.Infrastructure;

namespace OrderCore.Api.Shared;

/// <summary>
/// Registers the shared kernel's own services, the same way each module
/// registers its own through a <c>&lt;Module&gt;DependencyInjection</c>
/// extension method (section 5.1) — composed in Program.cs.
/// </summary>
public static class SharedDependencyInjection
{
    public static IServiceCollection AddSharedKernel(this IServiceCollection services)
    {
        services.AddScoped<IDomainEventDispatcher, InProcessDomainEventDispatcher>();

        return services;
    }
}
