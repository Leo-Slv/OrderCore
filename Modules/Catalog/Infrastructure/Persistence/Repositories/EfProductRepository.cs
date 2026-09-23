using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Modules.Catalog.Application.Contracts;
using OrderCore.Api.Modules.Catalog.Application.DTOs;
using OrderCore.Api.Modules.Catalog.Domain.Entities;
using OrderCore.Api.Modules.Catalog.Infrastructure.Persistence.Mappers;
using OrderCore.Api.Modules.Catalog.Infrastructure.Persistence.Models;

namespace OrderCore.Api.Modules.Catalog.Infrastructure.Persistence.Repositories;

/// <summary>
/// See EfCustomerRepository's remarks: reconciles the domain aggregate
/// against its tracked persistence model right before
/// <see cref="SaveChangesAsync"/>, since IProductRepository has no
/// explicit UpdateAsync either.
/// </summary>
public sealed class EfProductRepository : IProductRepository
{
    private readonly CatalogDbContext _dbContext;
    private readonly Dictionary<Guid, (Product Domain, ProductPersistenceModel Model)> _tracked = new();

    public EfProductRepository(CatalogDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Product?> GetByIdAsync(Guid productId, CancellationToken cancellationToken)
    {
        var model = await Query().FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);
        return model is null ? null : Track(model);
    }

    public async Task<Product?> GetBySkuAsync(string sku, CancellationToken cancellationToken)
    {
        var model = await Query().FirstOrDefaultAsync(p => p.Sku == sku, cancellationToken);
        return model is null ? null : Track(model);
    }

    public async Task<IReadOnlyList<Product>> ListAsync(ListProductsFilter filter, CancellationToken cancellationToken)
    {
        var query = Query();

        if (filter.CategoryId is { } categoryId)
        {
            query = query.Where(p => p.CategoryId == categoryId);
        }

        if (filter.Active is { } active)
        {
            query = query.Where(p => p.Active == active);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            query = query.Where(p => EF.Functions.ILike(p.Name, $"%{filter.SearchTerm}%"));
        }

        var models = await query
            .OrderBy(p => p.Name)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return models.Select(Track).ToList();
    }

    public async Task AddAsync(Product product, CancellationToken cancellationToken)
    {
        var model = ProductMapper.ToPersistence(product);
        await _dbContext.Products.AddAsync(model, cancellationToken);
        _tracked[product.Id] = (product, model);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        foreach (var (domain, model) in _tracked.Values)
        {
            ProductMapper.ApplyChanges(domain, model);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private Product Track(ProductPersistenceModel model)
    {
        var domain = ProductMapper.ToDomain(model);
        _tracked[domain.Id] = (domain, model);
        return domain;
    }

    private IQueryable<ProductPersistenceModel> Query() =>
        _dbContext.Products.Include(p => p.Images).Include(p => p.Variants);
}
