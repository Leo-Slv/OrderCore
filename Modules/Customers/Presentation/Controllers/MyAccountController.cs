using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderCore.Api.Modules.Customers.Application.DTOs;
using OrderCore.Api.Modules.Customers.Application.UseCases;
using OrderCore.Api.Modules.Customers.Presentation.Presenters;
using OrderCore.Api.Modules.Customers.Presentation.Requests;
using OrderCore.Api.Modules.Customers.Presentation.Responses;
using OrderCore.Api.Shared.Application.Abstractions;
using OrderCore.Api.Shared.Presentation.Authentication;

namespace OrderCore.Api.Modules.Customers.Presentation.Controllers;

/// <summary>
/// The signed-in customer's own profile and saved addresses. The customer
/// is always taken from the access token, never from the route or body, so
/// no one can reach another customer's data from here. An address id that
/// isn't the caller's answers 404, the same as one that doesn't exist.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.Customer)]
[Route("customers/me")]
public sealed class MyAccountController : ControllerBase
{
    private readonly GetCustomerByIdUseCase _getCustomer;
    private readonly UpdateCustomerProfileUseCase _updateProfile;
    private readonly ListCustomerAddressesUseCase _listAddresses;
    private readonly AddCustomerAddressUseCase _addAddress;
    private readonly GetCustomerAddressUseCase _getAddress;
    private readonly UpdateCustomerAddressUseCase _updateAddress;
    private readonly RemoveCustomerAddressUseCase _removeAddress;
    private readonly SetDefaultAddressUseCase _setDefaultAddress;
    private readonly ICurrentUser _currentUser;

    public MyAccountController(
        GetCustomerByIdUseCase getCustomer,
        UpdateCustomerProfileUseCase updateProfile,
        ListCustomerAddressesUseCase listAddresses,
        AddCustomerAddressUseCase addAddress,
        GetCustomerAddressUseCase getAddress,
        UpdateCustomerAddressUseCase updateAddress,
        RemoveCustomerAddressUseCase removeAddress,
        SetDefaultAddressUseCase setDefaultAddress,
        ICurrentUser currentUser)
    {
        _getCustomer = getCustomer;
        _updateProfile = updateProfile;
        _listAddresses = listAddresses;
        _addAddress = addAddress;
        _getAddress = getAddress;
        _updateAddress = updateAddress;
        _removeAddress = removeAddress;
        _setDefaultAddress = setDefaultAddress;
        _currentUser = currentUser;
    }

    /// <summary>The Customer policy guarantees the token carries a customer id.</summary>
    private Guid CustomerId => _currentUser.CustomerId!.Value;

    [HttpGet]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CustomerResponse>> GetProfileAsync(CancellationToken cancellationToken)
    {
        var customer = await _getCustomer.ExecuteAsync(CustomerId, cancellationToken);

        return Ok(CustomerPresenter.ToResponse(customer));
    }

    [HttpPut]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CustomerResponse>> UpdateProfileAsync(
        [FromBody] UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var customer = await _updateProfile.ExecuteAsync(CustomerId, request.Name, request.Phone, cancellationToken);

        return Ok(CustomerPresenter.ToResponse(customer));
    }

    [HttpGet("addresses")]
    [ProducesResponseType(typeof(IReadOnlyList<CustomerAddressResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CustomerAddressResponse>>> ListAddressesAsync(CancellationToken cancellationToken)
    {
        var addresses = await _listAddresses.ExecuteAsync(CustomerId, cancellationToken);

        return Ok(addresses.Select(CustomerPresenter.ToResponse).ToList());
    }

    [HttpPost("addresses")]
    [ProducesResponseType(typeof(CustomerAddressResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CustomerAddressResponse>> AddAddressAsync(
        [FromBody] CustomerAddressRequest request, CancellationToken cancellationToken)
    {
        var addressId = await _addAddress.ExecuteAsync(CustomerPresenter.ToCommand(CustomerId, request), cancellationToken);
        var address = await _getAddress.ExecuteAsync(CustomerId, addressId, cancellationToken);

        return CreatedAtAction(nameof(ListAddressesAsync), null, CustomerPresenter.ToResponse(address));
    }

    [HttpPut("addresses/{addressId:guid}")]
    [ProducesResponseType(typeof(CustomerAddressResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerAddressResponse>> UpdateAddressAsync(
        Guid addressId, [FromBody] CustomerAddressRequest request, CancellationToken cancellationToken)
    {
        var address = await _updateAddress.ExecuteAsync(CustomerPresenter.ToCommand(CustomerId, addressId, request), cancellationToken);

        return Ok(CustomerPresenter.ToResponse(address));
    }

    [HttpDelete("addresses/{addressId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveAddressAsync(Guid addressId, CancellationToken cancellationToken)
    {
        await _removeAddress.ExecuteAsync(CustomerId, addressId, cancellationToken);

        return NoContent();
    }

    [HttpPost("addresses/{addressId:guid}/default-shipping")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetDefaultShippingAddressAsync(Guid addressId, CancellationToken cancellationToken)
    {
        await _setDefaultAddress.ExecuteAsync(CustomerId, addressId, DefaultAddressKind.Shipping, cancellationToken);

        return NoContent();
    }

    [HttpPost("addresses/{addressId:guid}/default-billing")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetDefaultBillingAddressAsync(Guid addressId, CancellationToken cancellationToken)
    {
        await _setDefaultAddress.ExecuteAsync(CustomerId, addressId, DefaultAddressKind.Billing, cancellationToken);

        return NoContent();
    }
}
