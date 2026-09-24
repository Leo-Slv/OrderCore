using Microsoft.AspNetCore.Mvc;
using OrderCore.Api.Modules.Catalog.Application.DTOs;
using OrderCore.Api.Modules.Catalog.Application.UseCases;
using OrderCore.Api.Modules.Catalog.Presentation.Presenters;
using OrderCore.Api.Modules.Catalog.Presentation.Requests;
using OrderCore.Api.Modules.Catalog.Presentation.Responses;
using OrderCore.Api.Shared.Presentation.Responses;

namespace OrderCore.Api.Modules.Catalog.Presentation.Controllers;

/// <summary>
/// Thin endpoint delegating to the Catalog use cases (section 39) — same
/// [ApiController]/ControllerBase shape as CustomersController and
/// AuditLogsController (see the note on CustomersController about
/// 02-customers.md's CustomersEndpoints).
///
/// ChangeProductPriceUseCase has no route here: 03-catalog.md's
/// CatalogController never wires it to an endpoint, so it stays reachable
/// only from other Application-layer code until the diagram says
/// otherwise.
/// </summary>
[ApiController]
[Route("catalog")]
public sealed class CatalogController : ControllerBase
{
    private readonly CreateProductUseCase _createProductUseCase;
    private readonly UpdateProductUseCase _updateProductUseCase;
    private readonly PublishProductUseCase _publishProductUseCase;
    private readonly GetProductByIdUseCase _getProductByIdUseCase;
    private readonly GetProductBySlugUseCase _getProductBySlugUseCase;
    private readonly ListProductsUseCase _listProductsUseCase;
    private readonly CreateCategoryUseCase _createCategoryUseCase;
    private readonly ListCategoriesUseCase _listCategoriesUseCase;

    public CatalogController(
        CreateProductUseCase createProductUseCase,
        UpdateProductUseCase updateProductUseCase,
        PublishProductUseCase publishProductUseCase,
        GetProductByIdUseCase getProductByIdUseCase,
        GetProductBySlugUseCase getProductBySlugUseCase,
        ListProductsUseCase listProductsUseCase,
        CreateCategoryUseCase createCategoryUseCase,
        ListCategoriesUseCase listCategoriesUseCase)
    {
        _createProductUseCase = createProductUseCase;
        _updateProductUseCase = updateProductUseCase;
        _publishProductUseCase = publishProductUseCase;
        _getProductByIdUseCase = getProductByIdUseCase;
        _getProductBySlugUseCase = getProductBySlugUseCase;
        _listProductsUseCase = listProductsUseCase;
        _createCategoryUseCase = createCategoryUseCase;
        _listCategoriesUseCase = listCategoriesUseCase;
    }

    [HttpPost("products")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductResponse>> CreateProductAsync(
        [FromBody] CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var output = await _createProductUseCase.ExecuteAsync(ProductPresenter.ToCommand(request), cancellationToken);
        var response = ProductPresenter.ToResponse(output);

        return CreatedAtAction(nameof(GetProductByIdAsync), new { id = response.Id }, response);
    }

    [HttpPut("products/{id:guid}")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProductResponse>> UpdateProductAsync(
        Guid id,
        [FromBody] UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        var output = await _updateProductUseCase.ExecuteAsync(id, ProductPresenter.ToCommand(request), cancellationToken);

        return Ok(ProductPresenter.ToResponse(output));
    }

    [HttpPost("products/{id:guid}/publish")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PublishProductAsync(Guid id, CancellationToken cancellationToken)
    {
        await _publishProductUseCase.ExecuteAsync(id, cancellationToken);

        return NoContent();
    }

    [HttpGet("products/{id:guid}")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> GetProductByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var output = await _getProductByIdUseCase.ExecuteAsync(id, cancellationToken);

        return Ok(ProductPresenter.ToResponse(output));
    }

    /// <summary>
    /// Only published, active products are returned. See
    /// <see cref="GetProductBySlugUseCase"/>.
    /// </summary>
    [HttpGet("products/by-slug/{slug}")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> GetProductBySlugAsync(string slug, CancellationToken cancellationToken)
    {
        var output = await _getProductBySlugUseCase.ExecuteAsync(slug, cancellationToken);

        return Ok(ProductPresenter.ToResponse(output));
    }

    /// <summary>
    /// Returns every product matching the filter, drafts included. The
    /// storefront passes <c>active=true</c>; hiding drafts from public
    /// callers entirely waits for authentication (V2).
    /// </summary>
    [HttpGet("products")]
    [ProducesResponseType(typeof(PagedResponse<ProductSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<ProductSummaryResponse>>> ListProductsAsync(
        [FromQuery] ListProductsFilter filter,
        CancellationToken cancellationToken)
    {
        var products = await _listProductsUseCase.ExecuteAsync(filter, cancellationToken);

        return Ok(ProductPresenter.ToResponse(products));
    }

    [HttpPost("categories")]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryResponse>> CreateCategoryAsync(
        [FromBody] CreateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var output = await _createCategoryUseCase.ExecuteAsync(CategoryPresenter.ToCommand(request), cancellationToken);
        var response = CategoryPresenter.ToResponse(output);

        return CreatedAtAction(nameof(ListCategoriesAsync), null, response);
    }

    [HttpGet("categories")]
    [ProducesResponseType(typeof(IReadOnlyList<CategoryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoryResponse>>> ListCategoriesAsync(CancellationToken cancellationToken)
    {
        var categories = await _listCategoriesUseCase.ExecuteAsync(cancellationToken);

        return Ok(categories.Select(CategoryPresenter.ToResponse).ToList());
    }
}
