using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderCore.Api.Modules.Identity.Application.UseCases;
using OrderCore.Api.Modules.Identity.Presentation.Presenters;
using OrderCore.Api.Modules.Identity.Presentation.Requests;
using OrderCore.Api.Modules.Identity.Presentation.Responses;
using OrderCore.Api.Shared.Application.Abstractions;

namespace OrderCore.Api.Modules.Identity.Presentation.Controllers;

/// <summary>
/// Sign-up, sign-in, token refresh and sign-out. Tokens travel in the JSON
/// body, not in cookies set by the API; see <see cref="AuthTokensResponse"/>.
/// </summary>
[ApiController]
[Route("auth")]
public sealed class AuthController : ControllerBase
{
    private readonly SignUpCustomerUseCase _signUp;
    private readonly SignInUseCase _signIn;
    private readonly RefreshSessionUseCase _refresh;
    private readonly SignOutUseCase _signOut;
    private readonly ICurrentUser _currentUser;

    public AuthController(
        SignUpCustomerUseCase signUp, SignInUseCase signIn, RefreshSessionUseCase refresh, SignOutUseCase signOut, ICurrentUser currentUser)
    {
        _signUp = signUp;
        _signIn = signIn;
        _refresh = refresh;
        _signOut = signOut;
        _currentUser = currentUser;
    }

    /// <summary>Creates a customer account and signs it in.</summary>
    [HttpPost("sign-up")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthTokensResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthTokensResponse>> SignUpAsync([FromBody] SignUpRequest request, CancellationToken cancellationToken)
    {
        var tokens = await _signUp.ExecuteAsync(AuthPresenter.ToCommand(request), cancellationToken);

        return StatusCode(StatusCodes.Status201Created, AuthPresenter.ToResponse(tokens));
    }

    [HttpPost("sign-in")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthTokensResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthTokensResponse>> SignInAsync([FromBody] SignInRequest request, CancellationToken cancellationToken)
    {
        var tokens = await _signIn.ExecuteAsync(AuthPresenter.ToCommand(request), cancellationToken);

        return Ok(AuthPresenter.ToResponse(tokens));
    }

    /// <summary>
    /// Exchanges a refresh token for a new access/refresh pair. Anonymous on
    /// purpose: it is called exactly when the access token has expired.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthTokensResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthTokensResponse>> RefreshAsync([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var tokens = await _refresh.ExecuteAsync(request.RefreshToken, cancellationToken);

        return Ok(AuthPresenter.ToResponse(tokens));
    }

    /// <summary>Ends the caller's session the refresh token belongs to. Always 204.</summary>
    [HttpPost("sign-out")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SignOutAsync([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        await _signOut.ExecuteAsync(_currentUser.UserId!.Value, request.RefreshToken, cancellationToken);

        return NoContent();
    }
}
