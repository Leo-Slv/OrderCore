using OrderCore.Api.Modules.Orders.Application.UseCases;

namespace OrderCore.Api.Modules.Orders;

/// <summary>
/// Registers the Orders module's own services, the same way
/// <c>CoursesDependencyInjection</c> does for the Courses module in
/// CourseCore (section 5.1) — one extension method per module, composed in
/// Program.cs, instead of every use case being registered individually
/// there as the system grows.
/// </summary>
public static class OrdersDependencyInjection
{
    public static IServiceCollection AddOrdersModule(this IServiceCollection services)
    {
        services.AddScoped<CreateOrderHandler>();

        return services;
    }
}
