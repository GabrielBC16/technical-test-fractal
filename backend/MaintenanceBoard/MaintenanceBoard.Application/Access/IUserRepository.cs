using MaintenanceBoard.Domain.Access;

namespace MaintenanceBoard.Application.Access;

public interface IUserRepository
{
    Task<AppUser?> GetByIdAsync(int userId, CancellationToken cancellationToken = default);
    Task<UserCredentials?> GetCredentialsByUserNameAsync(string userName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AppUser>> GetActiveAssignableUsersAsync(CancellationToken cancellationToken = default);
}
