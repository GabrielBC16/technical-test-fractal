using MaintenanceBoard.Api.Contracts;
using MaintenanceBoard.Application.Access;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MaintenanceBoard.Api.Controllers;

/// <summary>Verifies credentials for the simulated user-header authentication flow.</summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService authentication) : ControllerBase
{
    /// <summary>Verifies a password hash and returns the user identity and role.</summary>
    /// <remarks>Send the returned userId as X-User-Id on subsequent requests. This endpoint does not issue a token.</remarks>
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginBody body, CancellationToken cancellationToken) =>
        Ok(await authentication.LoginAsync(new LoginRequest(body.UserName, body.Password), cancellationToken));
}
