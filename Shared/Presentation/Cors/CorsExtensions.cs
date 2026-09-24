namespace OrderCore.Api.Shared.Presentation.Cors;

/// <summary>
/// Lets a browser client on another origin (the Next.js storefront) call
/// the API. Allowed origins come only from configuration
/// (<c>Cors:AllowedOrigins</c>); an empty or missing list allows no
/// cross-origin requests at all, rather than falling back to "any origin".
/// </summary>
public static class CorsExtensions
{
    public const string StorefrontPolicy = "Storefront";
    public const string AllowedOriginsKey = "Cors:AllowedOrigins";

    public static IServiceCollection AddStorefrontCors(this IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigins = configuration.GetSection(AllowedOriginsKey).Get<string[]>() ?? [];

        services.AddCors(options => options.AddPolicy(StorefrontPolicy, policy => policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders("Location")));

        return services;
    }

    public static IApplicationBuilder UseStorefrontCors(this IApplicationBuilder app) => app.UseCors(StorefrontPolicy);
}
