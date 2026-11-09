using MaintenanceBoard.Application.Common;
using MaintenanceBoard.Domain.Access;

namespace MaintenanceBoard.Application.Access;

public sealed class UserService(IUserRepository users, ICurrentUser currentUser) : IUserService
{
    private readonly AuthenticatedUserResolver _actors = new(users, currentUser);

    public async Task<IReadOnlyList<AppUser>> GetAssignableUsersAsync(CancellationToken cancellationToken = default)
    {
        await _actors.RequireAsync(cancellationToken, UserRole.Operator, UserRole.Supervisor);
        var candidates = await users.GetActiveAssignableUsersAsync(cancellationToken);

        // User selection exposes only users eligible to receive assignments.
        return candidates
            .Where(user => user.IsActive && user.Role is UserRole.Operator or UserRole.Supervisor)
            .OrderBy(user => user.LastNames, StringComparer.Ordinal)
            .ThenBy(user => user.Names, StringComparer.Ordinal)
            .ThenBy(user => user.UserId)
            .ToArray();
    }
}
