using MaintenanceBoard.Domain.Access;

namespace MaintenanceBoard.Application.Access;

public sealed record LoginRequest(string? UserName, string? Password);

public sealed record LoginResponse(
    int UserId,
    string UserName,
    string Names,
    string LastNames,
    UserRole Role);
