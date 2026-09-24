using Microsoft.AspNetCore.Mvc;
using OrderCore.Api.Modules.Customers.Application.UseCases;
using OrderCore.Api.Modules.Customers.Presentation.Presenters;
using OrderCore.Api.Modules.Customers.Presentation.Requests;
using OrderCore.Api.Modules.Customers.Presentation.Responses;

namespace OrderCore.Api.Modules.Customers.Presentation.Controllers;

/// <summary>
/// Thin endpoint delegating to the Customers use cases (section 39).
/// 02-customers.md originally specified a minimal-API <c>CustomersEndpoints</c>
/// static class; this uses <see cref="ControllerBase"/> instead, matching
/// AuditLogsController — the one Presentation layer already implemented in
/// this codebase (section 33: prefer the established convention over
/// introducing a second one).
///
/// Failures thrown by the use cases (not found, duplicate, rule violated)
/// are not caught here: <c>ApiExceptionHandler</c> (Shared/Presentation)
/// turns them into the ProblemDetails responses the ProducesResponseType
/// attributes below document.
/// </summary>
[ApiController]
[Route("customers")]
public sealed class CustomersController : ControllerBase
{
    private readonly RegisterCustomerUseCase _registerCustomerUseCase;
    private readonly GetCustomerByIdUseCase _getCustomerByIdUseCase;
    private readonly AddCustomerAddressUseCase _addCustomerAddressUseCase;
    private readonly ListCustomerAddressesUseCase _listCustomerAddressesUseCase;

    public CustomersController(
        RegisterCustomerUseCase registerCustomerUseCase,
        GetCustomerByIdUseCase getCustomerByIdUseCase,
        AddCustomerAddressUseCase addCustomerAddressUseCase,
        ListCustomerAddressesUseCase listCustomerAddressesUseCase)
    {
        _registerCustomerUseCase = registerCustomerUseCase;
        _getCustomerByIdUseCase = getCustomerByIdUseCase;
        _addCustomerAddressUseCase = addCustomerAddressUseCase;
        _listCustomerAddressesUseCase = listCustomerAddressesUseCase;
    }

    [HttpPost]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CustomerResponse>> RegisterAsync(
        [FromBody] RegisterCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var output = await _registerCustomerUseCase.ExecuteAsync(CustomerPresenter.ToCommand(request), cancellationToken);
        var response = CustomerPresenter.ToResponse(output);

        return CreatedAtAction(nameof(GetByIdAsync), new { id = response.Id }, response);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var output = await _getCustomerByIdUseCase.ExecuteAsync(id, cancellationToken);

        return Ok(CustomerPresenter.ToResponse(output));
    }

    [HttpPost("{id:guid}/addresses")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddAddressAsync(
        Guid id,
        [FromBody] AddCustomerAddressRequest request,
        CancellationToken cancellationToken)
    {
        var addressId = await _addCustomerAddressUseCase.ExecuteAsync(CustomerPresenter.ToCommand(id, request), cancellationToken);

        return CreatedAtAction(nameof(ListAddressesAsync), new { id }, new { id = addressId });
    }

    [HttpGet("{id:guid}/addresses")]
    [ProducesResponseType(typeof(IReadOnlyList<CustomerAddressResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<CustomerAddressResponse>>> ListAddressesAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var addresses = await _listCustomerAddressesUseCase.ExecuteAsync(id, cancellationToken);

        return Ok(addresses.Select(CustomerPresenter.ToResponse).ToList());
    }
}
