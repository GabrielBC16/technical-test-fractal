using MaintenanceBoard.Application.Access;
using MaintenanceBoard.Application.Tickets;
using MaintenanceBoard.Tests.Fakes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

namespace MaintenanceBoard.Tests.Api;

public sealed class ApiTestFactory(
    string environmentName = "Development", bool enableSeed = false) : WebApplicationFactory<Program>
{
    internal TicketServiceContext Context { get; } = new();
    internal FakePasswordHasher Passwords { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environmentName);
        // HTTP tests must not depend on Windows Event Log write permissions.
        builder.ConfigureLogging(logging => logging.ClearProviders().AddConsole());
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:MaintenanceBoard"] = new SqlConnectionStringBuilder
                {
                    DataSource = "unused", InitialCatalog = "unused", IntegratedSecurity = true
                }.ConnectionString,
                ["Cors:AllowedOrigins:0"] = "https://frontend.example",
                ["DevelopmentSeed"] = enableSeed.ToString()
            }));
        builder.ConfigureTestServices(services =>
        {
            services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(2));
            Context.Users.Candidates = Context.Users.Users.Values.ToArray();
            Context.Users.Credentials["user20"] = new UserCredentials(Context.Users.Users[20], Passwords.ExpectedHash);
            services.RemoveAll<IUserRepository>();
            services.RemoveAll<ITicketRepository>();
            services.RemoveAll<IPasswordHasher>();
            services.AddSingleton<IUserRepository>(Context.Users);
            services.AddSingleton<ITicketRepository>(Context.Tickets);
            services.AddSingleton<IPasswordHasher>(Passwords);
        });
    }

    public HttpClient Client(string? userId = null)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://api.example"), AllowAutoRedirect = false
        });
        if (userId is not null)
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-User-Id", userId);
        }

        return client;
    }
}
