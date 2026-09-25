using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Modules.AuditLogs.Application.Services;
using OrderCore.Api.Modules.AuditLogs.Application.UseCases;
using OrderCore.Api.Modules.AuditLogs.Domain.Repositories;
using OrderCore.Api.Modules.AuditLogs.Infrastructure.Persistence;
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
    public static IServiceCollection AddAuditLogsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AuditLogsDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("OrderCoreDb")));

        services.AddScoped<IAuditLogRepository, EfAuditLogRepository>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<ListAuditLogsUseCase>();

        return services;
    }
}
