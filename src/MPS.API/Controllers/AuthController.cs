using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MPS.API.Requests.Auth;
using MPS.API.Responses.Auth;
using MPS.Application.Common.Models;
using MPS.Application.Features.Auth.Commands;
using MPS.Application.Features.Auth.Queries;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var command = new LoginCommand(request.Username, request.Password, GetClientIp());
        var tokens = await _mediator.Send(command, cancellationToken);
        return Ok(MapTokens(tokens));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-refresh")]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RefreshTokenCommand(request.RefreshToken, GetClientIp());
        var tokens = await _mediator.Send(command, cancellationToken);
        return Ok(MapTokens(tokens));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequest request,
        CancellationToken cancellationToken)
    {
        var command = new LogoutCommand(request.RefreshToken, GetClientIp());
        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ChangePasswordCommand(request.OldPassword, request.NewPassword, GetClientIp());
        var changed = await _mediator.Send(command, cancellationToken);
        if (!changed) return BadRequest(new { error = "Failed to change password." });
        return NoContent();
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-login")]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(new ForgotPasswordCommand(request.Email, GetClientIp()), cancellationToken);
        return Accepted(new { message = "If the email exists, a password reset link has been sent." });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-login")]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ResetPasswordCommand(request.Token, request.NewPassword, GetClientIp());
        var ok = await _mediator.Send(command, cancellationToken);
        if (!ok) return BadRequest(new { error = "Failed to reset password." });
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var user = await _mediator.Send(new GetCurrentUserQuery(), cancellationToken);
        return user is null ? Unauthorized() : Ok(user);
    }

    private string? GetClientIp() =>
        HttpContext.Connection.RemoteIpAddress?.ToString();

    private static AuthTokenResponse MapTokens(AuthTokenPair tokens) => new(
        tokens.AccessToken.Token,
        tokens.RefreshToken.Token,
        tokens.AccessToken.ExpiresAt,
        tokens.RefreshToken.ExpiresAt);
}
