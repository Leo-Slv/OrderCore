using OrderCore.Api.Modules.Catalog.Application.Contracts;
using OrderCore.Api.Modules.Catalog.Application.DTOs;
using OrderCore.Api.Modules.Catalog.Application.UseCases;
using OrderCore.Api.Modules.Inventory.Application.UseCases;

namespace OrderCore.Api.Modules.Catalog.Infrastructure.DemoData;

/// <summary>
/// The <c>seed-demo</c> command (<c>dotnet OrderCore.Api.dll seed-demo</c>):
/// fills the catalog with <see cref="DemoCatalog"/> through the same use cases
/// the backoffice uses — categories, products with their atelier as brand,
/// the "de" prices of the promotions, publishing — and receives each
/// product's stock through Inventory's use case, as an adapter would. It is
/// never run by itself: not by startup, not by <c>migrate</c>. Running it again
/// adds only what is missing (a category by name, a product by SKU), so it
/// never duplicates and never changes stock it already set.
/// </summary>
public sealed class DemoCatalogSeeder
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DemoCatalogSeeder> _logger;

    public DemoCatalogSeeder(IServiceScopeFactory scopeFactory, ILogger<DemoCatalogSeeder> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <returns><c>0</c> when the demo catalog is in place, <c>1</c> when a step failed.</returns>
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            var categories = await EnsureCategoriesAsync(cancellationToken);
            var created = 0;
            foreach (var product in DemoCatalog.Products)
            {
                if (await EnsureProductAsync(product, categories[product.Category], cancellationToken))
                {
                    created++;
                }
            }

            _logger.LogInformation(
                "Demo catalog ready: {Created} product(s) created, {Existing} already there.", created, DemoCatalog.Products.Count - created);
            return 0;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Seeding the demo catalog failed; running it again adds only what is missing.");
            return 1;
        }
    }

    private async Task<Dictionary<string, Guid>> EnsureCategoriesAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var existing = await scope.ServiceProvider.GetRequiredService<ListCategoriesUseCase>().ExecuteAsync(cancellationToken);
        var byName = existing
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

        var create = scope.ServiceProvider.GetRequiredService<CreateCategoryUseCase>();
        foreach (var name in DemoCatalog.Categories.Where(name => !byName.ContainsKey(name)))
        {
            var category = await create.ExecuteAsync(new CreateCategoryCommand(name, ParentCategoryId: null, Description: null), cancellationToken);
            byName[name] = category.Id;
            _logger.LogInformation("Created category {Category}.", name);
        }

        return byName;
    }

    /// <returns>Whether the product was created (false when its SKU already exists).</returns>
    private async Task<bool> EnsureProductAsync(DemoProduct demo, Guid categoryId, CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var services = scope.ServiceProvider;
        if (await services.GetRequiredService<IProductRepository>().GetBySkuAsync(demo.Sku, cancellationToken) is not null)
        {
            return false;
        }

        var product = await services.GetRequiredService<CreateProductUseCase>().ExecuteAsync(
            new CreateProductCommand(demo.Sku, demo.Name, categoryId, demo.Price, DemoCatalog.Currency), cancellationToken);
        await services.GetRequiredService<UpdateProductUseCase>().ExecuteAsync(
            product.Id, new UpdateProductCommand(demo.Name, demo.ShortDescription, demo.Description, demo.Maker), cancellationToken);
        if (demo.CompareAtPrice is { } compareAt)
        {
            await services.GetRequiredService<SetCompareAtPriceUseCase>().ExecuteAsync(product.Id, compareAt, cancellationToken);
        }

        await services.GetRequiredService<PublishProductUseCase>().ExecuteAsync(product.Id, cancellationToken);

        if (demo.ReorderLevel > 0)
        {
            await services.GetRequiredService<SetReorderLevelUseCase>().ExecuteAsync(product.Id, demo.ReorderLevel, cancellationToken);
        }

        if (demo.Stock > 0)
        {
            await services.GetRequiredService<ReceiveStockUseCase>().ExecuteAsync(
                product.Id, demo.Stock, "Estoque inicial da demonstração", cancellationToken);
        }

        _logger.LogInformation("Created product {Sku} with {Stock} unit(s) in stock.", demo.Sku, demo.Stock);
        return true;
    }
}
