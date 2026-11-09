using MaintenanceBoard.Domain.Access;

namespace MaintenanceBoard.Infrastructure.Development;

internal static class DefaultDevelopmentData
{
    public static DevelopmentSeedOptions CreateOptions() => new(
    [
        new("gbarrantes", "Gabriel", "Barrantes", UserRole.Requester, "gbarrantes"),
        new("fcabello", "Fernando", "Cabello", UserRole.Operator, "fcabello"),
        new("pramirez", "Pedro", "Ramirez", UserRole.Operator, "pramirez"),
        new("jcordova", "Juan Carlos", "Cordova", UserRole.Operator,"jcordova"),
        new("mhuarcaya", "Mario", "Huarcaya", UserRole.Supervisor, "mhuarcaya")
    ], IncludeSampleTickets: true);
}
