namespace OrderCore.Api.Modules.Customers.Presentation.Requests;

public sealed class RegisterCustomerRequest
{
    public string Name { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string? Phone { get; init; }

    public string? DocumentNumber { get; init; }
}
