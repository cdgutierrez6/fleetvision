using FleetVision.Identity.Application.Auth.Commands.Logout;
using FleetVision.Identity.Application.Auth.Commands.Refresh;
using FleetVision.Identity.Application.Auth.Commands.Register;
using FleetVision.Identity.Application.Auth.Commands.UpdateProfile;
using FleetVision.Identity.Application.Auth.Queries.GetCurrentUser;
using FleetVision.Identity.Application.Auth.Queries.Login;
using FleetVision.Identity.Domain.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace FleetVision.Identity.API.Controllers;

[ApiController]
[Route("auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator) => _mediator = mediator;

    [HttpPost("register")]
    [EnableRateLimiting("register")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Register([FromBody] RegisterCommand command, CancellationToken ct)
    {
        try
        {
            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (DuplicateEmailException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (DuplicateTenantSlugException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPost("login")]
    [EnableRateLimiting("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _mediator.Send(new LoginQuery(request.Email, request.Password), ct);
            return Ok(result);
        }
        catch (InvalidCredentialsException)
        {
            return Unauthorized(new { error = "Invalid email or password." });
        }
        catch (AccountInactiveException)
        {
            return Unauthorized(new { error = "Account is deactivated." });
        }
    }

    [HttpPost("refresh")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _mediator.Send(new RefreshCommand(request.RefreshToken), ct);
            return Ok(result);
        }
        catch (InvalidRefreshTokenException)
        {
            return Unauthorized(new { error = "Invalid or expired refresh token." });
        }
        catch (AccountInactiveException)
        {
            return Unauthorized(new { error = "Account is deactivated." });
        }
    }

    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request, CancellationToken ct)
    {
        await _mediator.Send(new LogoutCommand(request.RefreshToken), ct);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized();

        try
        {
            var result = await _mediator.Send(new GetCurrentUserQuery(userId.Value), ct);
            return Ok(result);
        }
        catch (UserNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPatch("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized();

        var result = await _mediator.Send(
            new UpdateProfileCommand(userId.Value, request.FirstName, request.LastName), ct);

        return Ok(result);
    }

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub");

        return Guid.TryParse(sub, out var id) ? id : null;
    }
}

// Request DTOs (solo para el controller — no son comandos de dominio)
public sealed record LoginRequest(string Email, string Password);
public sealed record RefreshRequest(string RefreshToken);
public sealed record UpdateProfileRequest(string FirstName, string LastName);
