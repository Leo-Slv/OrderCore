using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderCore.Api.Modules.Catalog.Application.DTOs;
using OrderCore.Api.Modules.Catalog.Application.UseCases;
using OrderCore.Api.Modules.Catalog.Presentation.Presenters;
using OrderCore.Api.Modules.Catalog.Presentation.Responses;
using OrderCore.Api.Shared.Presentation.Authentication;
using OrderCore.Api.Shared.Presentation.Responses;

namespace OrderCore.Api.Modules.Catalog.Presentation.Controllers;

/// <summary>
/// Backoffice reads that need a richer shape than the storefront's, under
/// <c>admin/</c> so each route has one response shape whoever calls it.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("admin/catalog")]
public sealed class CatalogAdminController : ControllerBase
{
    private readonly ListAdminProductsUseCase _listAdminProducts;

    public CatalogAdminController(ListAdminProductsUseCase listAdminProducts)
    {
        _listAdminProducts = listAdminProducts;
    }

    /// <summary>
    /// Every product (drafts and discontinued included), newest first, each
    /// with its stock figures; filter by <c>stock=LowStock|OutOfStock</c> for
    /// the stock screen.
    /// </summary>
    [HttpGet("products")]
    [ProducesResponseType(typeof(PagedResponse<AdminProductSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<AdminProductSummaryResponse>>> ListProductsAsync(
        [FromQuery] ListAdminProductsFilter filter, CancellationToken cancellationToken)
    {
        var page = await _listAdminProducts.ExecuteAsync(filter, cancellationToken);

        return Ok(AdminProductPresenter.ToResponse(page));
    }
}
