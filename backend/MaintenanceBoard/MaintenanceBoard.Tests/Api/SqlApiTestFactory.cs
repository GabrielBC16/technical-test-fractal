using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MaintenanceBoard.Tests.Api;

public sealed class SqlApiTestFactory(bool enableSeed = false) : WebApplicationFactory<Program>
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
            // Obsolete seed connection settings must never be read by startup.
            ["ConnectionStrings:Seed"] = "unused-invalid-legacy-value",
            ["DevelopmentSeed"] = enableSeed.ToString(),
            ["Cors:AllowedOrigins:0"] = "https://frontend.example"
        };
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(settings));
    }

    public HttpClient Client() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://api.example"), AllowAutoRedirect = false
    });
}
