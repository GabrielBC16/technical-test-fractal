using MaintenanceBoard.Infrastructure.Development;

namespace MaintenanceBoard.Api.Configuration;

internal static class DevelopmentSeedStartup
{
    public static async Task SeedDevelopmentDataAsync(this WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>("DevelopmentSeed"))
        {
            return;
        }

        if (!app.Environment.IsDevelopment())
        {
            throw new InvalidOperationException("Development seeding cannot be enabled outside Development.");
        }

        using var scope = app.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<DevelopmentSeeder>()
            .SeedAsync(app.Environment.EnvironmentName);
        app.Logger.LogInformation("Development seed completed: {CreatedUsers} users and {CreatedTickets} tickets.",
            result.CreatedUsers, result.CreatedTickets);
    }
}
