using OrderCore.Api.Modules.Catalog.Application.Contracts;
using OrderCore.Api.Modules.Catalog.Application.DTOs;

namespace OrderCore.Api.Modules.Catalog.Application.UseCases;

public sealed class ListCategoriesUseCase
{
    private readonly ICategoryRepository _categories;

    public ListCategoriesUseCase(ICategoryRepository categories)
    {
        _categories = categories;
    }

    public async Task<IReadOnlyList<CategoryOutput>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var categories = await _categories.ListAsync(cancellationToken);

        return categories.Select(CategoryOutput.From).ToList();
    }
}
