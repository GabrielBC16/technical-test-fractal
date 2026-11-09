using MaintenanceBoard.Application.Access;
using MaintenanceBoard.Domain.Access;
using MaintenanceBoard.Infrastructure.Development;
using MaintenanceBoard.Infrastructure.Persistence;

namespace MaintenanceBoard.Api.Configuration;

internal static class DevelopmentUserInitializer
{
    public static async Task InitializeDevelopmentUsersAsync(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment() || !app.Configuration.GetValue("DevelopmentUsers:Enabled", true))
        {
            return;
        }

        // Explicit local demo credentials requested for the technical test.
        // The seeder hashes passwords and never replaces an existing user's credentials.
        var options = new DevelopmentSeedOptions(
        [
            new("gbarrantes", "Gabriel", "Barrantes", UserRole.Requester, "gbarrantes"),
            new("fcabello", "Fernando", "Cabello", UserRole.Operator, "fcabello"),
            new("mhuarcaya", "Mario", "Huarcaya", UserRole.Supervisor, "mhuarcaya")
        ], IncludeSampleTickets: false);

        using var scope = app.Services.CreateScope();
        var seedConnection = app.Configuration.GetConnectionString("Seed");
        var seeder = string.IsNullOrWhiteSpace(seedConnection)
            ? scope.ServiceProvider.GetRequiredService<DevelopmentSeeder>()
            : new DevelopmentSeeder(new SqlConnectionFactory(seedConnection),
                scope.ServiceProvider.GetRequiredService<IPasswordHasher>());
        var result = await seeder.SeedAsync(app.Environment.EnvironmentName, options);
        app.Logger.LogInformation("Development user initialization completed: {CreatedUsers} users created.", result.CreatedUsers);
    }
}
