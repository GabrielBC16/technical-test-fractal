using MaintenanceBoard.Domain.Access;

namespace MaintenanceBoard.Infrastructure.Development;

public sealed record DevelopmentSeedUser(
    string UserName, string Names, string LastNames, UserRole Role, string Password);

public sealed record DevelopmentSeedOptions(
    IReadOnlyList<DevelopmentSeedUser> Users,
    bool IncludeSampleTickets = true);

public sealed record DevelopmentSeedResult(int CreatedUsers, int CreatedTickets);
