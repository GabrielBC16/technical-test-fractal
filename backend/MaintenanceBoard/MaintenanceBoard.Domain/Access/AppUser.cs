namespace MaintenanceBoard.Domain.Access;

public sealed record AppUser(
    int UserId,
    string UserName,
    string Names,
    string LastNames,
    UserRole Role,
    bool IsActive,
    DateTime CreatedAtUtc);
