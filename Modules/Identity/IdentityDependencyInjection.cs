using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Modules.Identity.Application.Contracts;
using OrderCore.Api.Modules.Identity.Application.UseCases;
using OrderCore.Api.Modules.Identity.Infrastructure.Adapters;
using OrderCore.Api.Modules.Identity.Infrastructure.Persistence;
using OrderCore.Api.Modules.Identity.Infrastructure.Persistence.Repositories;
using OrderCore.Api.Modules.Identity.Infrastructure.Security;

namespace OrderCore.Api.Modules.Identity;

/// <summary>
/// Registers the Identity module (technical, like AuditLogs): accounts,
/// credentials and sessions. Authentication middleware and policies are
/// wired separately, once endpoints use them.
/// </summary>
public static class IdentityDependencyInjection
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("OrderCoreDb")));

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(
                options => options.HasValidSigningKey,
                $"Jwt:SigningKey must be set (at least {JwtOptions.MinimumSigningKeyBytes} bytes), from the environment or user-secrets.");

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
