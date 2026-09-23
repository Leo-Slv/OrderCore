using OrderCore.Api.Modules.Catalog.Application.DTOs;
using OrderCore.Api.Modules.Catalog.Presentation.Requests;
using OrderCore.Api.Modules.Catalog.Presentation.Responses;

namespace OrderCore.Api.Modules.Catalog.Presentation.Presenters;

public static class CategoryPresenter
{
    public static CreateCategoryCommand ToCommand(CreateCategoryRequest request) => new(
        request.Name, request.ParentCategoryId, request.Description);

    public static CategoryResponse ToResponse(CategoryOutput output) => new()
    {
        Id = output.Id,
        Name = output.Name,
        Slug = output.Slug,
    };
}
