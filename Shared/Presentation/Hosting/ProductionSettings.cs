using Microsoft.Extensions.Options;

namespace OrderCore.Api.Shared.Presentation.Hosting;

/// <summary>The settings a deployment must provide (Docs/operations/deployment.md), as read at startup.</summary>
public sealed class ProductionSettings
{
    public string? ConnectionString { get; set; }

    public IReadOnlyList<string> AllowedOrigins { get; set; } = [];

    public string? AllowedHosts { get; set; }

    public string? BrokerHost { get; set; }

    public bool EmailApiConfigured { get; set; }

    public string? SmtpHost { get; set; }

    /// <summary>The storefront pages the account e-mails link to (<c>Identity:Links</c>).</summary>
    public IReadOnlyList<string?> AccountLinks { get; set; } = [];
}

/// <summary>
/// Outside Development, stops the API at startup when a setting a deployment
/// must provide is missing or still has its development value
/// (production-readiness spec, item 5): the database, the storefront origins
/// allowed by CORS, the host names the API answers for, the broker, a way to
/// send e-mail and the storefront links in account e-mails. Each failure
/// names the setting; nothing secret is echoed.
/// Development keeps its local defaults and isn't checked.
/// </summary>
public static class ProductionSettingsCheck
{
    public static IServiceCollection AddProductionSettingsCheck(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        if (environment.IsDevelopment())
        {
            return services;
        }

        services.AddOptions<ProductionSettings>()
            .Configure(settings =>
            {
                settings.ConnectionString = configuration.GetConnectionString("OrderCoreDb");
                settings.AllowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
                settings.AllowedHosts = configuration["AllowedHosts"];
                settings.BrokerHost = configuration["RabbitMq:Host"];
                settings.EmailApiConfigured = !string.IsNullOrWhiteSpace(configuration["Notifications:Resend:ApiKey"]);
                settings.SmtpHost = configuration["Notifications:Smtp:Host"];
                settings.AccountLinks = [configuration["Identity:Links:ResetPassword"], configuration["Identity:Links:ConfirmEmail"]];
            })
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<ProductionSettings>, ProductionSettingsValidator>();
        return services;
    }
}

public sealed class ProductionSettingsValidator : IValidateOptions<ProductionSettings>
{
    public ValidateOptionsResult Validate(string? name, ProductionSettings settings)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(settings.ConnectionString))
        {
            failures.Add("ConnectionStrings:OrderCoreDb is required.");
        }
        else if (PointsToLocalhost(settings.ConnectionString))
        {
            failures.Add("ConnectionStrings:OrderCoreDb still points to localhost (the development database).");
        }

        var origins = settings.AllowedOrigins.Where(o => !string.IsNullOrWhiteSpace(o)).ToList();
        if (origins.Count == 0)
        {
            failures.Add("Cors:AllowedOrigins must list the storefront's origin(s).");
        }
        else if (origins.Any(PointsToLocalhost))
        {
            failures.Add("Cors:AllowedOrigins still allows a localhost origin (development only).");
        }

        if (string.IsNullOrWhiteSpace(settings.AllowedHosts) || settings.AllowedHosts.Trim() == "*")
        {
            failures.Add("AllowedHosts must name the host(s) the API answers for (e.g. api.example.com), not *.");
        }

        if (string.IsNullOrWhiteSpace(settings.BrokerHost))
        {
            failures.Add("RabbitMq:Host is required.");
        }

        // Mailpit (localhost) only catches e-mail; a deployment must really send it.
        if (!settings.EmailApiConfigured && (string.IsNullOrWhiteSpace(settings.SmtpHost) || PointsToLocalhost(settings.SmtpHost)))
        {
            failures.Add("Notifications:Resend:ApiKey is required (or Notifications:Smtp:Host naming a real SMTP server, not localhost).");
        }

        // A missing link is reported by Identity's own check; here, only the local one.
        if (settings.AccountLinks.Any(link => !string.IsNullOrWhiteSpace(link) && PointsToLocalhost(link)))
        {
            failures.Add("Identity:Links still point to localhost: the account e-mails must link to the storefront.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static bool PointsToLocalhost(string value) =>
        value.Contains("localhost", StringComparison.OrdinalIgnoreCase)
        || value.Contains("127.0.0.1", StringComparison.Ordinal)
        || value.Contains("[::1]", StringComparison.Ordinal);
}
