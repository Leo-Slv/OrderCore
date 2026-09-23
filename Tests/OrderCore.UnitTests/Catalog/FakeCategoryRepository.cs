using OrderCore.Api.Modules.Catalog.Application.Contracts;
using OrderCore.Api.Modules.Catalog.Domain.Entities;

namespace OrderCore.UnitTests.Catalog;

internal sealed class FakeCategoryRepository : ICategoryRepository
{
    private readonly Dictionary<Guid, Category> _categories = new();

    public Task<Category?> GetByIdAsync(Guid categoryId, CancellationToken cancellationToken) =>
        Task.FromResult(_categories.GetValueOrDefault(categoryId));

    public Task<IReadOnlyList<Category>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Category>>(_categories.Values.ToList());

    public Task AddAsync(Category category, CancellationToken cancellationToken)
    {
        _categories[category.Id] = category;
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
