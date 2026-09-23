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
/// The use cases currently signal "not found" / "duplicate" with a plain
/// <see cref="InvalidOperationException"/>, which today reaches the client
/// as an unhandled 500 — there's no shared exception-handling convention
/// yet to map it to 404/409 (per claude.md: introduce one shared convention
/// rather than a try/catch per endpoint). The ProducesResponseType
/// attributes below document the intended contract, not yet-wired
/// behavior.
/// </summary>
[ApiController]
[Route("api/customers")]
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
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
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
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var output = await _getCustomerByIdUseCase.ExecuteAsync(id, cancellationToken);

        return Ok(CustomerPresenter.ToResponse(output));
    }

    [HttpPost("{id:guid}/addresses")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<CustomerAddressResponse>>> ListAddressesAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var addresses = await _listCustomerAddressesUseCase.ExecuteAsync(id, cancellationToken);

        return Ok(addresses.Select(CustomerPresenter.ToResponse).ToList());
    }
}
