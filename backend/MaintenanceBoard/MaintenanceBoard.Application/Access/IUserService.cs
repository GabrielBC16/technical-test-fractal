using MaintenanceBoard.Domain.Access;

namespace MaintenanceBoard.Application.Access;

public interface IUserService
{
    Task<IReadOnlyList<AppUser>> GetAssignableUsersAsync(CancellationToken cancellationToken = default);
}
