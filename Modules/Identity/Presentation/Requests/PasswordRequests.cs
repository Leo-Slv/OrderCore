namespace OrderCore.Api.Modules.Identity.Presentation.Requests;

public sealed class ForgotPasswordRequest
{
    public string Email { get; init; } = string.Empty;
}

public sealed class ResetPasswordRequest
{
    /// <summary>The token from the link in the reset e-mail.</summary>
    public string Token { get; init; } = string.Empty;

    public string NewPassword { get; init; } = string.Empty;
}

public sealed class ChangePasswordRequest
{
    public string CurrentPassword { get; init; } = string.Empty;

    public string NewPassword { get; init; } = string.Empty;

    /// <summary>The caller's refresh token: its session stays signed in, every other one ends.</summary>
    public string? RefreshToken { get; init; }
}
