using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Modules.Identity.Application.Contracts;
using OrderCore.Api.Modules.Identity.Application.UseCases;
using OrderCore.Api.Modules.Identity.Application.Telemetry;
using OrderCore.Api.Modules.Identity.Domain.Policies;
using OrderCore.Api.Modules.Identity.Infrastructure.Adapters;
using OrderCore.Api.Modules.Identity.Infrastructure.Hosting;
using OrderCore.Api.Modules.Identity.Infrastructure.Persistence;
using OrderCore.Api.Modules.Identity.Infrastructure.Persistence.Repositories;
using OrderCore.Api.Modules.Identity.Infrastructure.Security;
using OrderCore.Api.Shared.Infrastructure.Persistence;
using OrderCore.Api.Modules.Identity.Presentation;

namespace OrderCore.Api.Modules.Identity;

/// <summary>
/// Registers the Identity module (technical, like AuditLogs): accounts,
/// credentials and sessions, plus JWT bearer authentication (the tokens
/// this module issues) and the startup seed of the first admin. The
/// authorization policies are shared (<c>AuthorizationPolicies</c>).
/// </summary>
public static class IdentityDependencyInjection
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDatabaseMigrations<IdentityDbContext>(order: 10);
        services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("OrderCoreDb")));

        // Fails the start of the API, not the first sign-in, when the key is
        // missing or too short.
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(
                options => options.HasValidSigningKey,
                $"Jwt:SigningKey must be set (at least {JwtOptions.MinimumSigningKeyBytes} bytes), from the environment or user-secrets.")
            .ValidateOnStart();
        services.AddOrderCoreJwtBearer();
        services.AddIdentityRateLimits(configuration);

        // Identity:Lockout (MaxFailedAttempts, Duration); 5 in a row, 15 minutes by default.
        var lockout = configuration.GetSection("Identity:Lockout");
        services.AddSingleton(new LockoutPolicy(
            lockout.GetValue("MaxFailedAttempts", LockoutPolicy.Default.MaxFailedAttempts),
            lockout.GetValue("Duration", LockoutPolicy.Default.Duration)));
        services.AddSingleton<IdentityMetrics>();

        services.AddOptions<IdentitySeedOptions>().Bind(configuration.GetSection(IdentitySeedOptions.SectionName));
        services.AddHostedService<AdminSeedHostedService>();

        services.AddScoped<IUserAccountRepository, EfUserAccountRepository>();
        services.AddSingleton<IPasswordHasher, AspNetPasswordHasher>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        services.AddSingleton<IRefreshTokenGenerator, RandomRefreshTokenGenerator>();
        services.AddScoped<ICustomerRegistry, CustomerRegistryAdapter>();

        services.AddScoped<SignUpCustomerUseCase>();
        services.AddScoped<SignInUseCase>();
        services.AddScoped<RefreshSessionUseCase>();
        services.AddScoped<SignOutUseCase>();
        services.AddScoped<SeedAdminUseCase>();

        return services;
    }
}
