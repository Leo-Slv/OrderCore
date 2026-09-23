using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Modules.Orders.Application.UseCases;
using OrderCore.Api.Modules.Orders.Infrastructure.Adapters;
using OrderCore.Api.Modules.Orders.Infrastructure.Persistence;
using OrderCore.Api.Modules.Orders.Infrastructure.Persistence.Repositories;

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
    public static IServiceCollection AddOrdersModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<OrdersDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("OrderCoreDb")));

        services.AddScoped<IOrderRepository, EfOrderRepository>();
        services.AddScoped<IProductCatalog, ProductCatalogAdapter>();

        services.AddScoped<CreateOrderHandler>();

        return services;
    }
}
