using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Modules.Catalog.Application.Contracts;
using OrderCore.Api.Modules.Catalog.Domain.Entities;
using OrderCore.Api.Modules.Catalog.Infrastructure.Persistence.Mappers;
using OrderCore.Api.Modules.Catalog.Infrastructure.Persistence.Models;

namespace OrderCore.Api.Modules.Catalog.Infrastructure.Persistence.Repositories;

public sealed class EfCategoryRepository : ICategoryRepository
{
    private readonly CatalogDbContext _dbContext;
    private readonly Dictionary<Guid, (Category Domain, CategoryPersistenceModel Model)> _tracked = new();

    public EfCategoryRepository(CatalogDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Category?> GetByIdAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        var model = await _dbContext.Categories.FirstOrDefaultAsync(c => c.Id == categoryId, cancellationToken);
        return model is null ? null : Track(model);
    }

    public async Task<IReadOnlyList<Category>> ListAsync(CancellationToken cancellationToken)
    {
        var models = await _dbContext.Categories.OrderBy(c => c.DisplayOrder).ToListAsync(cancellationToken);
        return models.Select(Track).ToList();
    }

    public async Task AddAsync(Category category, CancellationToken cancellationToken)
    {
        var model = CategoryMapper.ToPersistence(category);
        await _dbContext.Categories.AddAsync(model, cancellationToken);
        _tracked[category.Id] = (category, model);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        foreach (var (domain, model) in _tracked.Values)
        {
            CategoryMapper.ApplyChanges(domain, model);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private Category Track(CategoryPersistenceModel model)
    {
        var domain = CategoryMapper.ToDomain(model);
        _tracked[domain.Id] = (domain, model);
        return domain;
    }
}
