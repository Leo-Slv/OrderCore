using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Modules.Orders.Application.UseCases;
using OrderCore.Api.Modules.Orders.Domain.Events;
using OrderCore.Api.Modules.Orders.Infrastructure.Adapters;
using OrderCore.Api.Modules.Orders.Infrastructure.EventHandlers;
using OrderCore.Api.Modules.Orders.Infrastructure.IntegrationEventHandlers;
using OrderCore.Api.Modules.Orders.Infrastructure.Persistence;
using OrderCore.Api.Modules.Orders.Infrastructure.Persistence.Repositories;
using OrderCore.Api.Modules.Payments.Application.Contracts.IntegrationEvents;
using OrderCore.Api.Shared.Application.Abstractions;

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
        services.AddScoped<IOrderNumberGenerator, SequentialOrderNumberGenerator>();
        services.AddScoped<IProductCatalog, ProductCatalogAdapter>();
        services.AddScoped<IInventoryService, InventoryServiceAdapter>();
        services.AddScoped<IPaymentGateway, PaymentGatewayAdapter>();
        services.AddScoped<ICustomerDirectory, CustomerDirectoryAdapter>();
        services.AddScoped<IOrderStatusHistoryReader, EfOrderStatusHistoryReader>();

        services.AddScoped<IDomainEventHandler<OrderCreated>, OrderStatusHistoryProjector>();
        services.AddScoped<IDomainEventHandler<OrderPaymentRequested>, OrderStatusHistoryProjector>();
        services.AddScoped<IDomainEventHandler<OrderConfirmed>, OrderStatusHistoryProjector>();
        services.AddScoped<IDomainEventHandler<OrderCancelled>, OrderStatusHistoryProjector>();
        services.AddScoped<IDomainEventHandler<OrderPaymentFailed>, OrderStatusHistoryProjector>();

        services.AddScoped<IDomainEventHandler<PaymentAuthorized>, PaymentAuthorizedIntegrationEventHandler>();
        services.AddScoped<IDomainEventHandler<PaymentFailed>, PaymentFailedIntegrationEventHandler>();

        services.AddScoped<CreateOrderHandler>();
        services.AddScoped<SetOrderAddressesUseCase>();
        services.AddScoped<RequestOrderPaymentUseCase>();
        services.AddScoped<ConfirmOrderUseCase>();
        services.AddScoped<MarkOrderPaymentFailedUseCase>();
        services.AddScoped<CancelOrderUseCase>();
        services.AddScoped<GetOrderByIdUseCase>();
        services.AddScoped<ListCustomerOrdersUseCase>();
        services.AddScoped<CheckoutUseCase>();
        services.AddScoped<QuoteCartUseCase>();
        services.AddScoped<GetOrderDetailsUseCase>();
        services.AddScoped<GetOrderStatusHistoryUseCase>();

        return services;
    }
}
