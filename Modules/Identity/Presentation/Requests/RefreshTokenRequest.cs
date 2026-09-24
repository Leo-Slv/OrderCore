namespace OrderCore.Api.Modules.Identity.Presentation.Requests;

/// <summary>Body of both <c>auth/refresh</c> and <c>auth/sign-out</c>.</summary>
public sealed class RefreshTokenRequest
{
    public string RefreshToken { get; init; } = string.Empty;
}
