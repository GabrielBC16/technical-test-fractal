using Dapper;
using MaintenanceBoard.Application.Access;
using MaintenanceBoard.Domain.Access;
using MaintenanceBoard.Infrastructure.Persistence;

namespace MaintenanceBoard.Infrastructure.Access;

public sealed class SqlUserRepository(SqlExecutor database) : IUserRepository
{
    public Task<AppUser?> GetByIdAsync(int userId, CancellationToken cancellationToken = default) =>
        database.ExecuteAsync(async (connection, token) =>
        {
            const string sql = """
                SELECT UserId, UserName, Names, LastNames, Role, IsActive, CreatedAtUtc
                FROM access.AppUser
                WHERE UserId = @UserId;
                """;
            var row = await connection.QuerySingleOrDefaultAsync<AppUserRow>(
                new CommandDefinition(sql, new { UserId = userId }, cancellationToken: token));
            return row?.ToUser();
        }, cancellationToken);

    public Task<UserCredentials?> GetCredentialsByUserNameAsync(string userName, CancellationToken cancellationToken = default) =>
        database.ExecuteAsync(async (connection, token) =>
        {
            const string sql = """
                SELECT UserId, UserName, PasswordHash, Names, LastNames, Role, IsActive, CreatedAtUtc
                FROM access.AppUser
                WHERE UserName = @UserName;
                """;
            var row = await connection.QuerySingleOrDefaultAsync<AppUserRow>(
                new CommandDefinition(sql, new { UserName = userName }, cancellationToken: token));
            return row is null ? null : new UserCredentials(row.ToUser(), row.PasswordHash);
        }, cancellationToken);

    public Task<IReadOnlyList<AppUser>> GetActiveAssignableUsersAsync(CancellationToken cancellationToken = default) =>
        database.ExecuteAsync<IReadOnlyList<AppUser>>(async (connection, token) =>
        {
            const string sql = """
                SELECT UserId, UserName, Names, LastNames, Role, IsActive, CreatedAtUtc
                FROM access.AppUser
                WHERE IsActive = 1 AND Role IN (N'Operator', N'Supervisor')
                ORDER BY LastNames, Names, UserId;
                """;
            var rows = await connection.QueryAsync<AppUserRow>(new CommandDefinition(sql, cancellationToken: token));
            return rows.Select(row => row.ToUser()).ToArray();
        }, cancellationToken);
}
