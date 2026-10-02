using HrServiceDesk.Api.Auth;
using HrServiceDesk.Api.ErrorHandling;
using HrServiceDesk.Application.Auth;
using HrServiceDesk.Application.Auth.Commands;
using HrServiceDesk.Application.Auth.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HrServiceDesk.Api.Controllers;

/// <summary>Sign-in, session refresh and sign-out. The refresh token is only ever exchanged via an HttpOnly cookie.</summary>
[Route("api/auth")]
public sealed class AuthController : ApiControllerBase
{
    public sealed record LoginRequest(string Email, string Password, bool RememberMe = false);

    public sealed record ForgotPasswordRequest(string Email);

    public sealed record ResetWithLinkRequest(string Email, string Token, string NewPassword);

    public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

    /// <summary>Access token for the SPA (kept in memory) plus the signed-in user's profile.</summary>
    public sealed record SessionResponse(string AccessToken, DateTimeOffset ExpiresAt, UserProfileDto User);

    /// <summary>Signs in with e-mail and password.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthSetup.AuthRateLimitPolicy)]
    [ProducesResponseType<SessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SessionResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new LoginCommand(request.Email, request.Password, request.RememberMe), cancellationToken);
        return ToSessionResponse(result);
    }

    /// <summary>Rotates the refresh token cookie and returns a new access token.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthSetup.AuthRateLimitPolicy)]
    [ProducesResponseType<SessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SessionResponse>> Refresh(CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new RefreshSessionCommand(RefreshTokenCookie.Read(Request)), cancellationToken);
        if (!result.IsSuccess)
            RefreshTokenCookie.Clear(Response);
        return ToSessionResponse(result);
    }

    /// <summary>Ends the current session and clears the refresh token cookie.</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Logout(CancellationToken cancellationToken)
    {
        await Sender.Send(new LogoutCommand(RefreshTokenCookie.Read(Request)), cancellationToken);
        RefreshTokenCookie.Clear(Response);
        return NoContent();
    }

    /// <summary>E-mails a password reset link if the address belongs to an active account (always 202).</summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthSetup.AuthRateLimitPolicy)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<ActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        await Sender.Send(new ForgotPasswordCommand(request.Email), cancellationToken);
        return Accepted();
    }

    /// <summary>Chooses a new password with the token from the e-mailed link.</summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthSetup.AuthRateLimitPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ResetPassword(ResetWithLinkRequest request, CancellationToken cancellationToken) =>
        FromResult(await Sender.Send(new ResetPasswordCommand(request.Email, request.Token, request.NewPassword), cancellationToken));

    /// <summary>The signed-in user's profile.</summary>
    [HttpGet("me")]
    [ProducesResponseType<UserProfileDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UserProfileDto>> Me(CancellationToken cancellationToken) =>
        FromResult(await Sender.Send(new GetCurrentUserQuery(), cancellationToken));

    /// <summary>Changes the caller's password and signs them out of every session.</summary>
    [HttpPost("change-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new ChangePasswordCommand(request.CurrentPassword, request.NewPassword), cancellationToken);
        if (result.IsSuccess)
            RefreshTokenCookie.Clear(Response);
        return FromResult(result);
    }

    private ActionResult<SessionResponse> ToSessionResponse(Application.Common.Results.Result<AuthSession> result)
    {
        if (!result.IsSuccess)
            return Problem(result.Error!);

        var session = result.Value;
        RefreshTokenCookie.Write(Response, session.RefreshToken, session.RefreshTokenExpiresAt, session.IsPersistent);
        return Ok(new SessionResponse(session.AccessToken, session.AccessTokenExpiresAt, session.User));
    }
}
