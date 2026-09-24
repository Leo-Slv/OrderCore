using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Modules.Customers.Application.Contracts;
using OrderCore.Api.Modules.Customers.Application.UseCases;
using OrderCore.Api.Modules.Customers.Infrastructure.Persistence;
using OrderCore.Api.Modules.Customers.Infrastructure.Persistence.Repositories;

namespace OrderCore.Api.Modules.Customers;

/// <summary>
/// Registers the Customers module's own services, the same way
/// <c>CoursesDependencyInjection</c> does for the Courses module in
/// CourseCore (section 5.1) — one extension method per module, composed in
/// Program.cs.
/// </summary>
public static class CustomersDependencyInjection
{
    public static IServiceCollection AddCustomersModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CustomersDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("OrderCoreDb")));

        services.AddScoped<ICustomerRepository, EfCustomerRepository>();

        services.AddScoped<RegisterCustomerUseCase>();
        services.AddScoped<UpdateCustomerProfileUseCase>();
        services.AddScoped<AddCustomerAddressUseCase>();
        services.AddScoped<ListCustomerAddressesUseCase>();
        services.AddScoped<GetCustomerAddressUseCase>();
        services.AddScoped<GetCustomerByIdUseCase>();

        return services;
    }
}
