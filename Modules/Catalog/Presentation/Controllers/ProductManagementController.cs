using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderCore.Api.Modules.Catalog.Application.UseCases;
using OrderCore.Api.Modules.Catalog.Presentation.Presenters;
using OrderCore.Api.Modules.Catalog.Presentation.Requests;
using OrderCore.Api.Modules.Catalog.Presentation.Responses;
using OrderCore.Api.Shared.Presentation.Authentication;

namespace OrderCore.Api.Modules.Catalog.Presentation.Controllers;

/// <summary>
/// The backoffice's changes to an existing product, beyond create/update/
/// publish (<see cref="CatalogController"/>): price, promotion,
/// discontinuing, images and variants. Admin-only. Each answers with the
/// updated product.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("catalog/products/{id:guid}")]
public sealed class ProductManagementController : ControllerBase
{
    private readonly ChangeProductPriceUseCase _changePrice;
    private readonly SetCompareAtPriceUseCase _setCompareAtPrice;
    private readonly DiscontinueProductUseCase _discontinue;
    private readonly ManageProductImagesUseCase _images;
    private readonly ManageProductVariantsUseCase _variants;

    public ProductManagementController(
        ChangeProductPriceUseCase changePrice,
        SetCompareAtPriceUseCase setCompareAtPrice,
        DiscontinueProductUseCase discontinue,
        ManageProductImagesUseCase images,
        ManageProductVariantsUseCase variants)
    {
        _changePrice = changePrice;
        _setCompareAtPrice = setCompareAtPrice;
        _discontinue = discontinue;
        _images = images;
        _variants = variants;
    }

    [HttpPut("price")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> ChangePriceAsync(
        Guid id, [FromBody] ChangeProductPriceRequest request, CancellationToken cancellationToken) =>
        Ok(ProductPresenter.ToResponse(await _changePrice.ExecuteAsync(id, request.NewPrice, cancellationToken)));

    [HttpPut("compare-at-price")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> SetCompareAtPriceAsync(
        Guid id, [FromBody] SetCompareAtPriceRequest request, CancellationToken cancellationToken) =>
        Ok(ProductPresenter.ToResponse(await _setCompareAtPrice.ExecuteAsync(id, request.CompareAtPrice, cancellationToken)));

    [HttpPost("discontinue")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> DiscontinueAsync(Guid id, CancellationToken cancellationToken) =>
        Ok(ProductPresenter.ToResponse(await _discontinue.ExecuteAsync(id, cancellationToken)));

    [HttpPost("images")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> AddImageAsync(
        Guid id, [FromBody] AddProductImageRequest request, CancellationToken cancellationToken) =>
        Ok(ProductPresenter.ToResponse(
            await _images.AddAsync(id, request.Url, request.AltText, request.IsPrimary, cancellationToken)));

    [HttpDelete("images/{imageId:guid}")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> RemoveImageAsync(Guid id, Guid imageId, CancellationToken cancellationToken) =>
        Ok(ProductPresenter.ToResponse(await _images.RemoveAsync(id, imageId, cancellationToken)));

    [HttpPut("images/order")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> ReorderImagesAsync(
        Guid id, [FromBody] ReorderProductImagesRequest request, CancellationToken cancellationToken) =>
        Ok(ProductPresenter.ToResponse(await _images.ReorderAsync(id, request.ImageIds, cancellationToken)));

    [HttpPost("variants")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> AddVariantAsync(
        Guid id, [FromBody] AddProductVariantRequest request, CancellationToken cancellationToken) =>
        Ok(ProductPresenter.ToResponse(await _variants.AddAsync(
            id, request.Sku, request.Name, JsonSerializer.Serialize(request.Attributes), request.AdditionalPrice, cancellationToken)));

    [HttpDelete("variants/{variantId:guid}")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> RemoveVariantAsync(Guid id, Guid variantId, CancellationToken cancellationToken) =>
        Ok(ProductPresenter.ToResponse(await _variants.RemoveAsync(id, variantId, cancellationToken)));
}
