using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Modules.Inventory.Contracts.IntegrationEvents;
using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Modules.Orders.Application.UseCases;
using OrderCore.Api.Modules.Orders.Domain.Events;
using OrderCore.Api.Modules.Orders.Infrastructure.Adapters;
using OrderCore.Api.Modules.Orders.Infrastructure.EventHandlers;
using OrderCore.Api.Modules.Orders.Infrastructure.IntegrationEventHandlers;
using OrderCore.Api.Modules.Orders.Infrastructure.Messaging;
using OrderCore.Api.Modules.Orders.Infrastructure.Persistence;
using OrderCore.Api.Modules.Orders.Infrastructure.Persistence.Repositories;
using OrderCore.Api.Modules.Payments.Contracts.IntegrationEvents;
using OrderCore.Api.Shared.Application.Abstractions;
using OrderCore.Api.Shared.Infrastructure.Messaging;
using IntegrationEvents = OrderCore.Api.Modules.Orders.Contracts.IntegrationEvents;

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
    public const string PaymentOutcomesQueue = "orders.payment-outcomes";

    public const string TimelineQueue = "orders.timeline";

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
        services.AddScoped<IOrderTimelineReader, EfOrderTimelineReader>();

        services.AddScoped<IDomainEventHandler<OrderCreated>, OrderStatusHistoryProjector>();
        services.AddScoped<IDomainEventHandler<OrderPaymentRequested>, OrderStatusHistoryProjector>();
        services.AddScoped<IDomainEventHandler<OrderConfirmed>, OrderStatusHistoryProjector>();
        services.AddScoped<IDomainEventHandler<OrderCancelled>, OrderStatusHistoryProjector>();
        services.AddScoped<IDomainEventHandler<OrderPaymentFailed>, OrderStatusHistoryProjector>();
        services.AddScoped<IDomainEventHandler<OrderProcessingStarted>, OrderStatusHistoryProjector>();
        services.AddScoped<IDomainEventHandler<OrderShipped>, OrderStatusHistoryProjector>();
        services.AddScoped<IDomainEventHandler<OrderDelivered>, OrderStatusHistoryProjector>();

        // What Orders publishes, through its own outbox (Docs/specs/events).
        services.AddScoped<OutboxWriter<OrdersDbContext>>();
        services.AddScoped<IOrdersOutbox, OrdersOutbox>();
        services.AddOutboxSource<OrdersDbContext>();
        services.AddIntegrationEvent<IntegrationEvents.OrderCreated>(IntegrationEvents.OrderCreated.Name, 1);
        services.AddIntegrationEvent<IntegrationEvents.OrderPaymentRequested>(IntegrationEvents.OrderPaymentRequested.Name, 1);
        services.AddIntegrationEvent<IntegrationEvents.OrderConfirmed>(IntegrationEvents.OrderConfirmed.Name, 1);
        services.AddIntegrationEvent<IntegrationEvents.OrderProcessingStarted>(IntegrationEvents.OrderProcessingStarted.Name, 1);
        services.AddIntegrationEvent<IntegrationEvents.OrderShipped>(IntegrationEvents.OrderShipped.Name, 1);
        services.AddIntegrationEvent<IntegrationEvents.OrderDelivered>(IntegrationEvents.OrderDelivered.Name, 1);
        services.AddIntegrationEvent<IntegrationEvents.OrderPaymentFailed>(IntegrationEvents.OrderPaymentFailed.Name, 1);
        services.AddIntegrationEvent<IntegrationEvents.OrderCancelled>(IntegrationEvents.OrderCancelled.Name, 1);

        // Payment outcomes arrive through RabbitMQ; the inbox lives in OrdersDbContext.
        services.AddIntegrationEventConsumer<PaymentAuthorized, PaymentAuthorizedIntegrationEventHandler, OrdersDbContext>(
            PaymentOutcomesQueue);
        services.AddIntegrationEventConsumer<PaymentFailed, PaymentFailedIntegrationEventHandler, OrdersDbContext>(
            PaymentOutcomesQueue);

        // The admin order timeline: every event about an order, from every module.
        services.AddIntegrationEventConsumer<IntegrationEvents.OrderCreated, OrderTimelineProjector, OrdersDbContext>(TimelineQueue);
        services.AddIntegrationEventConsumer<IntegrationEvents.OrderPaymentRequested, OrderTimelineProjector, OrdersDbContext>(TimelineQueue);
        services.AddIntegrationEventConsumer<IntegrationEvents.OrderConfirmed, OrderTimelineProjector, OrdersDbContext>(TimelineQueue);
        services.AddIntegrationEventConsumer<IntegrationEvents.OrderProcessingStarted, OrderTimelineProjector, OrdersDbContext>(TimelineQueue);
        services.AddIntegrationEventConsumer<IntegrationEvents.OrderShipped, OrderTimelineProjector, OrdersDbContext>(TimelineQueue);
        services.AddIntegrationEventConsumer<IntegrationEvents.OrderDelivered, OrderTimelineProjector, OrdersDbContext>(TimelineQueue);
        services.AddIntegrationEventConsumer<IntegrationEvents.OrderPaymentFailed, OrderTimelineProjector, OrdersDbContext>(TimelineQueue);
        services.AddIntegrationEventConsumer<IntegrationEvents.OrderCancelled, OrderTimelineProjector, OrdersDbContext>(TimelineQueue);
        services.AddIntegrationEventConsumer<PaymentRequested, OrderTimelineProjector, OrdersDbContext>(TimelineQueue);
        services.AddIntegrationEventConsumer<PaymentAuthorized, OrderTimelineProjector, OrdersDbContext>(TimelineQueue);
        services.AddIntegrationEventConsumer<PaymentFailed, OrderTimelineProjector, OrdersDbContext>(TimelineQueue);
        services.AddIntegrationEventConsumer<PaymentCaptured, OrderTimelineProjector, OrdersDbContext>(TimelineQueue);
        services.AddIntegrationEventConsumer<PaymentVoided, OrderTimelineProjector, OrdersDbContext>(TimelineQueue);
        services.AddIntegrationEventConsumer<PaymentRefunded, OrderTimelineProjector, OrdersDbContext>(TimelineQueue);
        services.AddIntegrationEventConsumer<StockReserved, OrderTimelineProjector, OrdersDbContext>(TimelineQueue);
        services.AddIntegrationEventConsumer<StockReleased, OrderTimelineProjector, OrdersDbContext>(TimelineQueue);
        services.AddIntegrationEventConsumer<StockConsumed, OrderTimelineProjector, OrdersDbContext>(TimelineQueue);
        services.AddIntegrationEventConsumer<StockReturned, OrderTimelineProjector, OrdersDbContext>(TimelineQueue);

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
        services.AddScoped<GetOrderTimelineUseCase>();
        services.AddScoped<FulfilOrderUseCase>();
        services.AddScoped<SetOrderInternalNotesUseCase>();
        services.AddScoped<ListOrdersUseCase>();
        services.AddScoped<GetAdminOrderDetailsUseCase>();
        services.AddScoped<GetDashboardUseCase>();

        return services;
    }
}
