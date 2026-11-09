using MaintenanceBoard.Domain.Access;
using MaintenanceBoard.Infrastructure.Persistence;

namespace MaintenanceBoard.Infrastructure.Access;

internal sealed class AppUserRow
{
    public int UserId { get; set; }
    public string UserName { get; set; } = "";
    public string Names { get; set; } = "";
    public string LastNames { get; set; } = "";
    public string Role { get; set; } = "";
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string PasswordHash { get; set; } = "";

    public AppUser ToUser() => new(
        UserId, UserName, Names, LastNames, SqlValues.ParseEnum<UserRole>(Role), IsActive, SqlValues.AsUtc(CreatedAtUtc));
}
