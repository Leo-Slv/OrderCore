using OrderCore.Api.Modules.Catalog.Domain.Entities;

namespace OrderCore.Api.Modules.Catalog.Application.Contracts;

public interface ICategoryRepository
{
    Task<Category?> GetByIdAsync(Guid categoryId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Category>> ListAsync(CancellationToken cancellationToken);

    Task AddAsync(Category category, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
