using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Modules.Inventory.Application.Contracts;
using OrderCore.Api.Modules.Inventory.Application.UseCases;
using OrderCore.Api.Modules.Inventory.Domain.Events;
using OrderCore.Api.Modules.Inventory.Infrastructure.EventHandlers;
using OrderCore.Api.Modules.Inventory.Infrastructure.Persistence;
using OrderCore.Api.Modules.Inventory.Infrastructure.Persistence.Repositories;
using OrderCore.Api.Shared.Application.Abstractions;

namespace OrderCore.Api.Modules.Inventory;

/// <summary>
/// Registers the Inventory module's own services — see
/// CustomersDependencyInjection's remarks on the per-module extension
/// method convention.
/// </summary>
public static class InventoryDependencyInjection
{
    public static IServiceCollection AddInventoryModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<InventoryDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("OrderCoreDb")));

        // Both the concrete type and the public interface resolve to the
        // same Scoped instance: InventoryUnitOfWork needs the concrete
        // types (for their internal IPendingChangesTracker), while use
        // cases depend on the public interfaces.
        services.AddScoped<EfStockItemRepository>();
        services.AddScoped<IStockItemRepository>(sp => sp.GetRequiredService<EfStockItemRepository>());
        services.AddScoped<EfInventoryReservationRepository>();
        services.AddScoped<IInventoryReservationRepository>(sp => sp.GetRequiredService<EfInventoryReservationRepository>());
        services.AddScoped<IUnitOfWork, InventoryUnitOfWork>();

        services.AddScoped<IDomainEventHandler<InventoryStockMovementRecorded>, StockMovementRecorder>();

        services.AddScoped<ReserveStockUseCase>();
        services.AddScoped<ReleaseReservationUseCase>();
        services.AddScoped<ConsumeReservationUseCase>();
        services.AddScoped<ExpireReservationUseCase>();
        services.AddScoped<AdjustStockUseCase>();
        services.AddScoped<GetStockByProductIdUseCase>();
        services.AddScoped<GetStockAvailabilityUseCase>();

        return services;
    }
}
