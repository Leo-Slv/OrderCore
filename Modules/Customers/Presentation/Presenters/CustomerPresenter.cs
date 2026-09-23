using OrderCore.Api.Modules.Customers.Application.DTOs;
using OrderCore.Api.Modules.Customers.Domain.Entities;
using OrderCore.Api.Modules.Customers.Presentation.Requests;
using OrderCore.Api.Modules.Customers.Presentation.Responses;
using OrderCore.Api.Shared.Domain.ValueObjects;

namespace OrderCore.Api.Modules.Customers.Presentation.Presenters;

public static class CustomerPresenter
{
    public static RegisterCustomerCommand ToCommand(RegisterCustomerRequest request) => new(
        request.Name, request.Email, request.Phone, request.DocumentNumber, request.PasswordHash);

    public static AddCustomerAddressCommand ToCommand(Guid customerId, AddCustomerAddressRequest request) => new(
        customerId,
        request.Label,
        request.RecipientName,
        request.Phone,
        Address.Create(
            request.Street,
            request.Number,
            request.Complement,
            request.Neighborhood,
            request.City,
            request.State,
            request.PostalCode,
            request.Country));

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
        City = address.Address.City,
        IsDefaultShipping = address.IsDefaultShipping,
    };
}
