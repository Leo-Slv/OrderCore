using OrderCore.Api.Modules.Identity.Application.DTOs;
using OrderCore.Api.Modules.Identity.Presentation.Requests;
using OrderCore.Api.Modules.Identity.Presentation.Responses;

namespace OrderCore.Api.Modules.Identity.Presentation.Presenters;

public static class AuthPresenter
{
    public static SignUpCommand ToCommand(SignUpRequest request) => new(request.Name, request.Email, request.Password, request.Phone);

    public static SignInCommand ToCommand(SignInRequest request) => new(request.Email, request.Password);

    public static AuthTokensResponse ToResponse(AuthTokens tokens) => new()
    {
        UserId = tokens.UserId,
        Role = tokens.Role,
        CustomerId = tokens.CustomerId,
        AccessToken = tokens.AccessToken,
        AccessTokenExpiresAt = tokens.AccessTokenExpiresAt,
        RefreshToken = tokens.RefreshToken,
        RefreshTokenExpiresAt = tokens.RefreshTokenExpiresAt,
    };
}
