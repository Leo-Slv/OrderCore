using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderCore.Api.Modules.Customers.Application.DTOs;
using OrderCore.Api.Modules.Customers.Application.UseCases;
using OrderCore.Api.Modules.Customers.Presentation.Presenters;
using OrderCore.Api.Modules.Customers.Presentation.Requests;
using OrderCore.Api.Modules.Customers.Presentation.Responses;
using OrderCore.Api.Shared.Presentation.Authentication;
using OrderCore.Api.Shared.Presentation.Responses;

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
///
/// Admin-only. Customers register through <c>POST auth/sign-up</c>
/// (Identity module), which creates the customer; their own profile and
/// addresses are served under <c>customers/me</c>.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("customers")]
public sealed class CustomersController : ControllerBase
{
    private readonly GetCustomerByIdUseCase _getCustomerByIdUseCase;
    private readonly AddCustomerAddressUseCase _addCustomerAddressUseCase;
    private readonly ListCustomerAddressesUseCase _listCustomerAddressesUseCase;
    private readonly ListCustomersUseCase _listCustomersUseCase;
    private readonly ChangeCustomerStatusUseCase _changeCustomerStatusUseCase;

    public CustomersController(
        GetCustomerByIdUseCase getCustomerByIdUseCase,
        AddCustomerAddressUseCase addCustomerAddressUseCase,
        ListCustomerAddressesUseCase listCustomerAddressesUseCase,
        ListCustomersUseCase listCustomersUseCase,
        ChangeCustomerStatusUseCase changeCustomerStatusUseCase)
    {
        _getCustomerByIdUseCase = getCustomerByIdUseCase;
        _addCustomerAddressUseCase = addCustomerAddressUseCase;
        _listCustomerAddressesUseCase = listCustomerAddressesUseCase;
        _listCustomersUseCase = listCustomersUseCase;
        _changeCustomerStatusUseCase = changeCustomerStatusUseCase;
    }

    /// <summary>
    /// Newest first; <c>searchTerm</c> matches name or e-mail. A customer's
    /// orders are at <c>GET orders/customers/{customerId}</c>.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<CustomerResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<CustomerResponse>>> ListAsync(
        [FromQuery] ListCustomersFilter filter, CancellationToken cancellationToken)
    {
        var page = await _listCustomersUseCase.ExecuteAsync(filter, cancellationToken);

        return Ok(CustomerPresenter.ToResponse(page));
    }

    /// <summary>
    /// The customer can no longer sign in, refresh a session or check out.
    /// Deactivating an inactive customer changes nothing.
    /// </summary>
    [HttpPost("{id:guid}/deactivate")]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerResponse>> DeactivateAsync(Guid id, CancellationToken cancellationToken) =>
        Ok(CustomerPresenter.ToResponse(await _changeCustomerStatusUseCase.DeactivateAsync(id, cancellationToken)));

    [HttpPost("{id:guid}/reactivate")]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerResponse>> ReactivateAsync(Guid id, CancellationToken cancellationToken) =>
        Ok(CustomerPresenter.ToResponse(await _changeCustomerStatusUseCase.ReactivateAsync(id, cancellationToken)));

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
        [FromBody] CustomerAddressRequest request,
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
