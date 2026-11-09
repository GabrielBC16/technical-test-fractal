using MaintenanceBoard.Application.Common;
using MaintenanceBoard.Application.Common.Exceptions;
using MaintenanceBoard.Domain.Access;

namespace MaintenanceBoard.Application.Access;

internal sealed class AuthenticatedUserResolver(IUserRepository users, ICurrentUser currentUser)
{
    public async Task<AppUser> RequireAsync(CancellationToken cancellationToken, params UserRole[] allowedRoles)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (currentUser.UserId is not int userId || userId <= 0)
        {
            throw new UnauthenticatedUserException();
        }

        var user = await users.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            throw new UnauthenticatedUserException();
        }

        if (!Enum.IsDefined(user.Role) || (allowedRoles.Length > 0 && !allowedRoles.Contains(user.Role)))
        {
            throw new ForbiddenOperationException();
        }

        return user;
    }
}
