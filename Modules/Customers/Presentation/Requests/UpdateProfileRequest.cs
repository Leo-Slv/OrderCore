namespace OrderCore.Api.Modules.Customers.Presentation.Requests;

/// <summary>
/// What a customer can change about themselves. The e-mail is the sign-in
/// identity and stays as it is.
/// </summary>
public sealed class UpdateProfileRequest
{
    public string Name { get; init; } = string.Empty;

    public string? Phone { get; init; }
}
