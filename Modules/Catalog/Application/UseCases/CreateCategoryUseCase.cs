using OrderCore.Api.Modules.Catalog.Application.Contracts;
using OrderCore.Api.Modules.Catalog.Application.DTOs;
using OrderCore.Api.Modules.Catalog.Domain.Entities;
using OrderCore.Api.Shared.Domain.ValueObjects;

namespace OrderCore.Api.Modules.Catalog.Application.UseCases;

public sealed class CreateCategoryUseCase
{
    private readonly ICategoryRepository _categories;
    private readonly TimeProvider _timeProvider;

    public CreateCategoryUseCase(ICategoryRepository categories, TimeProvider timeProvider)
    {
        _categories = categories;
        _timeProvider = timeProvider;
    }

    public async Task<CategoryOutput> ExecuteAsync(CreateCategoryCommand command, CancellationToken cancellationToken)
    {
        if (command.ParentCategoryId is { } parentCategoryId)
        {
            _ = await _categories.GetByIdAsync(parentCategoryId, cancellationToken)
                ?? throw new InvalidOperationException($"Parent category '{parentCategoryId}' was not found.");
        }

        var now = _timeProvider.GetUtcNow();
        var category = Category.Create(
            command.Name, Slug.GenerateFrom(command.Name), command.ParentCategoryId, command.Description, now);

        await _categories.AddAsync(category, cancellationToken);
        await _categories.SaveChangesAsync(cancellationToken);

        return CategoryOutput.From(category);
    }
}
