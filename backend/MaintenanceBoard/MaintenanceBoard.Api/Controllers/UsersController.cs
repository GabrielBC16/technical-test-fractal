using MaintenanceBoard.Application.Access;
using MaintenanceBoard.Domain.Access;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MaintenanceBoard.Api.Controllers;

/// <summary>Provides active users eligible to receive ticket assignments.</summary>
[ApiController]
[Authorize(Roles = "Operator,Supervisor")]
[Route("api/users")]
public sealed class UsersController(IUserService users) : ControllerBase
{
    /// <summary>Lists active Operators and Supervisors for assignee selection.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AppUser>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<AppUser>>> GetAssignableUsers(CancellationToken cancellationToken) =>
        Ok(await users.GetAssignableUsersAsync(cancellationToken));
}
