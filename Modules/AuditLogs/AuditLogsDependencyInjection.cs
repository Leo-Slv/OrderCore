using OrderCore.Api.Modules.AuditLogs.Application.Services;
using OrderCore.Api.Modules.AuditLogs.Application.UseCases;
using OrderCore.Api.Modules.AuditLogs.Domain.Repositories;
using OrderCore.Api.Modules.AuditLogs.Infrastructure.Persistence.Repositories;

namespace OrderCore.Api.Modules.AuditLogs;

/// <summary>
/// Registers the AuditLogs module's own services, the same way
/// <c>CoursesDependencyInjection</c>/<c>AuditLogsDependencyInjection</c> do
/// in CourseCore (section 5.1) — one extension method per module, composed
/// in Program.cs.
/// </summary>
public static class AuditLogsDependencyInjection
{
    public static IServiceCollection AddAuditLogsModule(this IServiceCollection services)
    {
        // Singleton: the in-memory store (section 40) must survive across
        // requests for the duration of the process. Swap for a Scoped
        // registration when this becomes an EF-backed repository.
        services.AddSingleton<IAuditLogRepository, InMemoryAuditLogRepository>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<ListAuditLogsUseCase>();

        return services;
    }
}
