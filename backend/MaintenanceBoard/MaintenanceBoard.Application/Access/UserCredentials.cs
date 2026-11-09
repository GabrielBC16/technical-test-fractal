using MaintenanceBoard.Domain.Access;

namespace MaintenanceBoard.Application.Access;

// Persistence-only credentials. Never return this type from an API endpoint.
public sealed record UserCredentials(AppUser User, string PasswordHash);
