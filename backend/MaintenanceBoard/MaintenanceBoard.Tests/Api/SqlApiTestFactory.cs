using MaintenanceBoard.Infrastructure.Development;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MaintenanceBoard.Tests.Api;

public sealed class SqlApiTestFactory(DevelopmentSeedOptions? seed = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var connectionString = Environment.GetEnvironmentVariable("MAINTENANCEBOARD_TEST_CONNECTION_STRING")
            ?? throw new InvalidOperationException("Configure the isolated SQL Server test connection.");
        if (!new SqlConnectionStringBuilder(connectionString).InitialCatalog
            .StartsWith("MaintenanceBoard_Validation", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("HTTP integration tests require an isolated MaintenanceBoard_Validation database.");
        }

        builder.UseEnvironment("Development");
        builder.ConfigureLogging(logging => logging.ClearProviders().AddConsole());
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:MaintenanceBoard"] = connectionString,
            ["ConnectionStrings:Seed"] = "",
            ["DevelopmentSeed:Enabled"] = (seed is not null).ToString(),
            ["DevelopmentSeed:IncludeSampleTickets"] = (seed?.IncludeSampleTickets ?? false).ToString(),
            ["Cors:AllowedOrigins:0"] = "https://frontend.example"
        };
        if (seed is not null)
        {
            for (var index = 0; index < seed.Users.Count; index++)
            {
                var user = seed.Users[index];
                var prefix = $"DevelopmentSeed:Users:{index}:";
                settings[prefix + "UserName"] = user.UserName;
                settings[prefix + "Names"] = user.Names;
                settings[prefix + "LastNames"] = user.LastNames;
                settings[prefix + "Role"] = user.Role.ToString();
                settings[prefix + "Password"] = user.Password;
            }
        }

        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(settings));
    }

    public HttpClient Client() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://api.example"), AllowAutoRedirect = false
    });
}
