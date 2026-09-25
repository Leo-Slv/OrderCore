using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Modules.Catalog.Application.Contracts;
using OrderCore.Api.Modules.Catalog.Application.UseCases;
using OrderCore.Api.Modules.Catalog.Infrastructure.Adapters;
using OrderCore.Api.Modules.Catalog.Infrastructure.Persistence;
using OrderCore.Api.Modules.Catalog.Infrastructure.Persistence.Repositories;

namespace OrderCore.Api.Modules.Catalog;

/// <summary>
/// Registers the Catalog module's own services — see
/// CustomersDependencyInjection's remarks on the per-module extension
/// method convention.
/// </summary>
public static class CatalogDependencyInjection
{
    public static IServiceCollection AddCatalogModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CatalogDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("OrderCoreDb")));

        services.AddScoped<IProductRepository, EfProductRepository>();
        services.AddScoped<ICategoryRepository, EfCategoryRepository>();
        // One adapter instance behind both contracts.
        services.AddScoped<InventoryStockAvailabilityAdapter>();
        services.AddScoped<IStockAvailabilityProvider>(sp => sp.GetRequiredService<InventoryStockAvailabilityAdapter>());
        services.AddScoped<IStockLevels>(sp => sp.GetRequiredService<InventoryStockAvailabilityAdapter>());

        services.AddScoped<CreateProductUseCase>();
        services.AddScoped<UpdateProductUseCase>();
        services.AddScoped<ChangeProductPriceUseCase>();
        services.AddScoped<PublishProductUseCase>();
        services.AddScoped<GetProductByIdUseCase>();
        services.AddScoped<GetProductBySlugUseCase>();
        services.AddScoped<ListProductsUseCase>();
        services.AddScoped<ListAdminProductsUseCase>();
        services.AddScoped<SetCompareAtPriceUseCase>();
        services.AddScoped<DiscontinueProductUseCase>();
        services.AddScoped<ManageProductImagesUseCase>();
        services.AddScoped<ManageProductVariantsUseCase>();
        services.AddScoped<CreateCategoryUseCase>();
        services.AddScoped<ListCategoriesUseCase>();

        return services;
    }
}
