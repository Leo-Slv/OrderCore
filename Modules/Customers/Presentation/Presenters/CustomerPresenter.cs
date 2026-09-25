using OrderCore.Api.Modules.Customers.Application.DTOs;
using OrderCore.Api.Modules.Customers.Domain.Entities;
using OrderCore.Api.Modules.Customers.Presentation.Requests;
using OrderCore.Api.Modules.Customers.Presentation.Responses;
using OrderCore.Api.Shared.Domain.ValueObjects;

namespace OrderCore.Api.Modules.Customers.Presentation.Presenters;

public static class CustomerPresenter
{
    public static AddCustomerAddressCommand ToCommand(Guid customerId, CustomerAddressRequest request) => new(
        customerId, request.Label, request.RecipientName, request.Phone, ToAddress(request));

    public static UpdateCustomerAddressCommand ToCommand(Guid customerId, Guid addressId, CustomerAddressRequest request) => new(
        customerId, addressId, request.Label, request.RecipientName, request.Phone, ToAddress(request));

    private static Address ToAddress(CustomerAddressRequest request) => Address.Create(
        request.Street,
        request.Number,
        request.Complement,
        request.Neighborhood,
        request.City,
        request.State,
        request.PostalCode,
        request.Country);

    public static CustomerResponse ToResponse(CustomerOutput output) => new()
    {
        Id = output.Id,
        Name = output.Name,
        Email = output.Email,
        Active = output.Active,
    };

    public static CustomerAddressResponse ToResponse(CustomerAddress address) => new()
    {
        Id = address.Id,
        Label = address.Label,
        RecipientName = address.RecipientName,
        Phone = address.Phone,
        Street = address.Address.Street,
        Number = address.Address.Number,
        Complement = address.Address.Complement,
        Neighborhood = address.Address.Neighborhood,
        City = address.Address.City,
        State = address.Address.State,
        PostalCode = address.Address.PostalCode,
        Country = address.Address.Country,
        IsDefaultShipping = address.IsDefaultShipping,
        IsDefaultBilling = address.IsDefaultBilling,
    };
}
