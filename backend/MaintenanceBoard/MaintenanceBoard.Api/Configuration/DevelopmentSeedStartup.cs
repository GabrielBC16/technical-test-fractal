using MaintenanceBoard.Domain.Access;
using MaintenanceBoard.Infrastructure.Development;
using MaintenanceBoard.Infrastructure.Persistence;
using MaintenanceBoard.Application.Access;

namespace MaintenanceBoard.Api.Configuration;

internal static class DevelopmentSeedStartup
{
    public static async Task SeedDevelopmentDataAsync(this WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>("DevelopmentSeed:Enabled"))
        {
            return;
        }

        if (!app.Environment.IsDevelopment())
        {
            throw new InvalidOperationException("Development seeding cannot be enabled outside Development.");
        }

        using var scope = app.Services.CreateScope();
        var users = app.Configuration.GetSection("DevelopmentSeed:Users").GetChildren().Select(section =>
        {
            var roleText = section["Role"];
            if (!Enum.TryParse<UserRole>(roleText, ignoreCase: false, out var role) ||
                !Enum.IsDefined(role) || role.ToString() != roleText)
            {
                throw new InvalidOperationException("Each development seed user needs a valid Requester, Operator, or Supervisor role.");
            }

            return new DevelopmentSeedUser(section["UserName"] ?? "", section["Names"] ?? "",
                section["LastNames"] ?? "", role, section["Password"] ?? "");
        }).ToArray();
        var options = new DevelopmentSeedOptions(users,
            app.Configuration.GetValue("DevelopmentSeed:IncludeSampleTickets", true));

        var seedConnection = app.Configuration.GetConnectionString("Seed");
        var seeder = string.IsNullOrWhiteSpace(seedConnection)
            ? scope.ServiceProvider.GetRequiredService<DevelopmentSeeder>()
            : new DevelopmentSeeder(new SqlConnectionFactory(seedConnection),
                scope.ServiceProvider.GetRequiredService<IPasswordHasher>());
        var result = await seeder.SeedAsync(app.Environment.EnvironmentName, options);
        app.Logger.LogInformation("Development seed completed: {CreatedUsers} users and {CreatedTickets} tickets.",
            result.CreatedUsers, result.CreatedTickets);
    }
}
