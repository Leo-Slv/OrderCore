using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Modules.Catalog.Application.Contracts;
using OrderCore.Api.Modules.Catalog.Application.DTOs;
using OrderCore.Api.Modules.Catalog.Domain.Entities;
using OrderCore.Api.Modules.Catalog.Domain.Enums;
using OrderCore.Api.Modules.Catalog.Infrastructure.Persistence.Mappers;
using OrderCore.Api.Modules.Catalog.Infrastructure.Persistence.Models;
using OrderCore.Api.Shared.Domain.ValueObjects;

namespace OrderCore.Api.Modules.Catalog.Infrastructure.Persistence.Repositories;

/// <summary>
/// See EfCustomerRepository's remarks: reconciles the domain aggregate
/// against its tracked persistence model right before
/// <see cref="SaveChangesAsync"/>, since IProductRepository has no
/// explicit UpdateAsync either.
/// </summary>
public sealed class EfProductRepository : IProductRepository
{
    private static readonly string PublishedStatus = ProductStatus.Active.ToString();

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

    public async Task<Product?> GetBySlugAsync(Slug slug, CancellationToken cancellationToken)
    {
        var model = await Query().FirstOrDefaultAsync(p => p.Slug == slug.Value, cancellationToken);
        return model is null ? null : Track(model);
    }

    public async Task<IReadOnlyList<Product>> ListByIdsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken)
    {
        var models = await Query().AsSplitQuery().Where(p => productIds.Contains(p.Id)).ToListAsync(cancellationToken);
        return models.Select(Track).ToList();
    }

    public async Task<(IReadOnlyList<Product> Items, int TotalCount)> ListAsync(
        ListProductsFilter filter, bool publishedOnly, CancellationToken cancellationToken)
    {
        IQueryable<ProductPersistenceModel> query = _dbContext.Products;

        if (publishedOnly)
        {
            query = query.Where(p => p.Status == PublishedStatus && p.Active);
        }

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

        if (filter.OnSale is { } onSale)
        {
            query = onSale
                ? query.Where(p => p.CompareAtPrice != null && p.CompareAtPrice > p.CurrentPrice)
                : query.Where(p => p.CompareAtPrice == null || p.CompareAtPrice <= p.CurrentPrice);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var models = await Sort(query, filter.Sort)
            .Include(p => p.Images)
            .Include(p => p.Variants)
            .AsSplitQuery()
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return (models.Select(Track).ToList(), totalCount);
    }

    public async Task<(IReadOnlyList<Product> Items, int TotalCount)> ListForAdminAsync(
        ListAdminProductsFilter filter, IReadOnlyCollection<Guid>? onlyProductIds, CancellationToken cancellationToken)
    {
        IQueryable<ProductPersistenceModel> query = _dbContext.Products;

        if (onlyProductIds is not null)
        {
            query = query.Where(p => onlyProductIds.Contains(p.Id));
        }

        if (filter.Status is { } status)
        {
            var statusName = status.ToString();
            query = query.Where(p => p.Status == statusName);
        }

        if (filter.CategoryId is { } categoryId)
        {
            query = query.Where(p => p.CategoryId == categoryId);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var pattern = $"%{filter.SearchTerm.Trim()}%";
            query = query.Where(p => EF.Functions.ILike(p.Name, pattern) || EF.Functions.ILike(p.Sku, pattern));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var models = await query
            .OrderByDescending(p => p.CreatedAt)
            .ThenBy(p => p.Id)
            .Include(p => p.Images)
            .Include(p => p.Variants)
            .AsSplitQuery()
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return (models.Select(Track).ToList(), totalCount);
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

    /// <summary>
    /// Every order ends with <c>Id</c> as a tie-breaker, so products with
    /// equal names/prices/dates keep a stable order and don't appear on two
    /// pages (or on none) between requests.
    /// </summary>
    private static IQueryable<ProductPersistenceModel> Sort(IQueryable<ProductPersistenceModel> query, ProductSortOrder sort) => sort switch
    {
        ProductSortOrder.PriceAsc => query.OrderBy(p => p.CurrentPrice).ThenBy(p => p.Id),
        ProductSortOrder.PriceDesc => query.OrderByDescending(p => p.CurrentPrice).ThenBy(p => p.Id),
        ProductSortOrder.Newest => query
            .OrderBy(p => p.PublishedAt == null)
            .ThenByDescending(p => p.PublishedAt)
            .ThenByDescending(p => p.CreatedAt)
            .ThenBy(p => p.Id),
        _ => query.OrderBy(p => p.Name).ThenBy(p => p.Id),
    };

    private Product Track(ProductPersistenceModel model)
    {
        var domain = ProductMapper.ToDomain(model);
        _tracked[domain.Id] = (domain, model);
        return domain;
    }

    private IQueryable<ProductPersistenceModel> Query() =>
        _dbContext.Products.Include(p => p.Images).Include(p => p.Variants);
}
