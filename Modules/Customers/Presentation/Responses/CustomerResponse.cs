namespace OrderCore.Api.Modules.Customers.Presentation.Responses;

public sealed class CustomerResponse
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string? Phone { get; init; }

    public bool Active { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}
