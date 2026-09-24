namespace OrderCore.Api.Modules.Identity.Presentation.Requests;

public sealed class SignUpRequest
{
    public string Name { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    /// <summary>8 to 128 characters, at least one letter and one digit.</summary>
    public string Password { get; init; } = string.Empty;

    public string? Phone { get; init; }
}
