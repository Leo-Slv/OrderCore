using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OrderCore.Api.Modules.Identity.Application.UseCases;
using OrderCore.Api.Modules.Identity.Presentation.Presenters;
using OrderCore.Api.Modules.Identity.Presentation.Requests;
using OrderCore.Api.Modules.Identity.Presentation.Responses;
using OrderCore.Api.Shared.Application.Abstractions;
using OrderCore.Api.Shared.Presentation.Authentication;

namespace OrderCore.Api.Modules.Identity.Presentation.Controllers;

/// <summary>
/// Sign-up, sign-in, token refresh, sign-out and the password: forgot, reset
/// and change (Docs/specs/identity/password-recovery.md). Tokens travel in
/// the JSON body, not in cookies set by the API; see <see cref="AuthTokensResponse"/>.
/// </summary>
[ApiController]
[Route("auth")]
public sealed class AuthController : ControllerBase
{
    private readonly SignUpCustomerUseCase _signUp;
    private readonly SignInUseCase _signIn;
    private readonly RefreshSessionUseCase _refresh;
    private readonly SignOutUseCase _signOut;
    private readonly RequestPasswordResetUseCase _requestPasswordReset;
    private readonly ResetPasswordUseCase _resetPassword;
    private readonly ChangePasswordUseCase _changePassword;
    private readonly ConfirmEmailUseCase _confirmEmail;
    private readonly RequestEmailConfirmationUseCase _requestEmailConfirmation;
    private readonly ICurrentUser _currentUser;

    public AuthController(
        SignUpCustomerUseCase signUp,
        SignInUseCase signIn,
        RefreshSessionUseCase refresh,
        SignOutUseCase signOut,
        RequestPasswordResetUseCase requestPasswordReset,
        ResetPasswordUseCase resetPassword,
        ChangePasswordUseCase changePassword,
        ConfirmEmailUseCase confirmEmail,
        RequestEmailConfirmationUseCase requestEmailConfirmation,
        ICurrentUser currentUser)
    {
        _confirmEmail = confirmEmail;
        _requestEmailConfirmation = requestEmailConfirmation;
        _signUp = signUp;
        _signIn = signIn;
        _refresh = refresh;
        _signOut = signOut;
        _requestPasswordReset = requestPasswordReset;
        _resetPassword = resetPassword;
        _changePassword = changePassword;
        _currentUser = currentUser;
    }

    /// <summary>Creates a customer account and signs it in.</summary>
    [HttpPost("sign-up")]
    [AllowAnonymous]
    [EnableRateLimiting(IdentityRateLimits.SignUp)]
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
    [EnableRateLimiting(IdentityRateLimits.SignIn)]
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
    [EnableRateLimiting(IdentityRateLimits.Refresh)]
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

    /// <summary>
    /// Asks for a password-reset e-mail. Always 202, whether the address has an
    /// account or not, so the form can't tell who has one.
    /// </summary>
    [HttpPost("password/forgot")]
    [AllowAnonymous]
    [EnableRateLimiting(IdentityRateLimits.ForgotPassword)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ForgotPasswordAsync([FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        await _requestPasswordReset.ExecuteAsync(request.Email, cancellationToken);

        return Accepted();
    }

    /// <summary>Sets a new password with the token from the reset e-mail; every session of the account ends.</summary>
    [HttpPost("password/reset")]
    [AllowAnonymous]
    [EnableRateLimiting(IdentityRateLimits.ResetPassword)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ResetPasswordAsync([FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        await _resetPassword.ExecuteAsync(request.Token, request.NewPassword, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Confirms the e-mail address with the token from the confirmation e-mail.
    /// Refresh the session afterwards: the new access token is the one that
    /// lets the customer check out.
    /// </summary>
    [HttpPost("email/confirm")]
    [AllowAnonymous]
    [EnableRateLimiting(IdentityRateLimits.ConfirmEmail)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConfirmEmailAsync([FromBody] ConfirmEmailRequest request, CancellationToken cancellationToken)
    {
        await _confirmEmail.ExecuteAsync(request.Token, cancellationToken);

        return NoContent();
    }

    /// <summary>Sends the signed-in customer a new confirmation link; the previous one stops working.</summary>
    [HttpPost("email/confirmation")]
    [Authorize(Policy = AuthorizationPolicies.Customer)]
    [EnableRateLimiting(IdentityRateLimits.EmailConfirmation)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RequestEmailConfirmationAsync(CancellationToken cancellationToken)
    {
        await _requestEmailConfirmation.ExecuteAsync(_currentUser.UserId!.Value, cancellationToken);

        return Accepted();
    }

    /// <summary>
    /// Replaces the signed-in user's password, confirming the current one. The
    /// session of the given refresh token stays; every other one ends.
    /// </summary>
    [HttpPost("password/change")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangePasswordAsync([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        await _changePassword.ExecuteAsync(
            _currentUser.UserId!.Value, request.CurrentPassword, request.NewPassword, request.RefreshToken, cancellationToken);

        return NoContent();
    }
}
