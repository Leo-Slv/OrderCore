namespace OrderCore.Api.Shared.Infrastructure.Hosting;

/// <summary>
/// What one running copy of the API does (Docs/specs/operations/vercel-hosting.md).
/// <see cref="All"/> is the default and the way OrderCore has always run: HTTP and
/// every background service in one process. A platform that scales HTTP
/// separately from always-on work (Vercel: container Functions plus Daemons)
/// runs the same image twice: <see cref="Web"/> answers requests only,
/// <see cref="Worker"/> runs the background services (outbox relay, consumers,
/// e-mail dispatcher, jobs). Run a single <see cref="Worker"/>: those services
/// assume one instance (Docs/operations/deployment.md).
/// </summary>
public enum HostingRole
{
    All,
    Web,
    Worker,
}

public static class HostingRoles
{
    public const string SettingName = "Hosting:Role";

    /// <summary>Reads <c>Hosting:Role</c> (case-insensitive, default <c>All</c>); anything else stops the API at startup.</summary>
    public static HostingRole Read(IConfiguration configuration)
    {
        var value = configuration[SettingName];
        if (string.IsNullOrWhiteSpace(value))
        {
            return HostingRole.All;
        }

        if (Enum.TryParse<HostingRole>(value.Trim(), ignoreCase: true, out var role) && Enum.IsDefined(role))
        {
            return role;
        }

        throw new InvalidOperationException($"{SettingName} must be All, Web or Worker (it is '{value}').");
    }

    /// <summary>Whether this copy runs the background services: every role but <see cref="HostingRole.Web"/>.</summary>
    public static bool RunsBackgroundServices(this HostingRole role) => role != HostingRole.Web;

    /// <summary>
    /// Registers a background service only in a copy that runs them
    /// (<c>Hosting:Role</c> All or Worker), so a Web copy stays a plain HTTP server.
    /// </summary>
    public static IServiceCollection AddBackgroundWorker<TService>(this IServiceCollection services, IConfiguration configuration)
        where TService : class, IHostedService
    {
        if (Read(configuration).RunsBackgroundServices())
        {
            services.AddHostedService<TService>();
        }

        return services;
    }
}
