using Microsoft.Extensions.Options;
using OrderCore.Api.Modules.Identity.Application.UseCases;

namespace OrderCore.Api.Modules.Identity.Infrastructure.Hosting;

/// <summary>
/// Bound from <c>IdentitySeed</c>. Both values come from the environment
/// (<c>IdentitySeed__AdminEmail</c>/<c>IdentitySeed__AdminPassword</c>),
/// never from a committed settings file.
/// </summary>
public sealed class IdentitySeedOptions
{
    public const string SectionName = "IdentitySeed";

    public string? AdminEmail { get; set; }

    public string? AdminPassword { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(AdminEmail) && !string.IsNullOrWhiteSpace(AdminPassword);
}

/// <summary>
/// Runs <see cref="SeedAdminUseCase"/> once at startup when a seed admin is
/// configured; without configuration it doesn't touch the database. A
/// failure (e.g. migrations not applied yet) is logged rather than
/// stopping the API, since migrations are never applied from startup and
/// the next start retries.
/// </summary>
public sealed class AdminSeedHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IdentitySeedOptions _options;
    private readonly ILogger<AdminSeedHostedService> _logger;

    public AdminSeedHostedService(
        IServiceScopeFactory scopeFactory, IOptions<IdentitySeedOptions> options, ILogger<AdminSeedHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.IsConfigured)
        {
            _logger.LogInformation(
                "No seed admin configured (IdentitySeed:AdminEmail/AdminPassword); no administrator will be created.");
            return;
        }

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var seedAdmin = scope.ServiceProvider.GetRequiredService<SeedAdminUseCase>();
            if (await seedAdmin.ExecuteAsync(_options.AdminEmail!, _options.AdminPassword!, cancellationToken))
            {
                _logger.LogInformation("Seed administrator {Email} created.", _options.AdminEmail);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Could not seed the administrator account; the API starts without it.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
