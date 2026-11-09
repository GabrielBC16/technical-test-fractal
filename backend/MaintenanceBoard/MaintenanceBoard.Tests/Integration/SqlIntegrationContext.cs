using MaintenanceBoard.Application.Access;
using MaintenanceBoard.Domain.Access;
using MaintenanceBoard.Infrastructure.Access;
using MaintenanceBoard.Infrastructure.Development;
using MaintenanceBoard.Infrastructure.Persistence;
using MaintenanceBoard.Infrastructure.Tickets;
using Microsoft.Data.SqlClient;

namespace MaintenanceBoard.Tests.Integration;

internal sealed class SqlIntegrationContext
{
    public SqlConnectionFactory Connections { get; }
    public SqlExecutor Database { get; }
    public SqlTicketRepository Tickets { get; }
    public SqlUserRepository Users { get; }
    public BCryptPasswordHasher Passwords { get; } = new(workFactor: 4);
    public DevelopmentSeedOptions Options { get; }
    public DevelopmentSeeder Seeder { get; }
    public DevelopmentSeedResult SeedResult { get; private set; } = new(0, 0);
    public string Password { get; } = Guid.NewGuid().ToString("N") + "Aa1!";
    public AppUser Requester { get; private set; } = null!;
    public AppUser Operator { get; private set; } = null!;
    public AppUser Supervisor { get; private set; } = null!;
    public AppUser SecondOperator { get; private set; } = null!;
    public AppUser ThirdOperator { get; private set; } = null!;

    private SqlIntegrationContext(bool includeSamples)
    {
        var connectionString = Environment.GetEnvironmentVariable("MAINTENANCEBOARD_TEST_CONNECTION_STRING")
            ?? throw new InvalidOperationException("Configure the isolated SQL Server test connection.");
        var settings = new SqlConnectionStringBuilder(connectionString);
        if (!settings.InitialCatalog.StartsWith("MaintenanceBoard_Validation", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Integration tests require an isolated MaintenanceBoard_Validation database.");
        }

        Connections = new SqlConnectionFactory(connectionString);
        Database = new SqlExecutor(Connections);
        Tickets = new SqlTicketRepository(Database);
        Users = new SqlUserRepository(Database);
        Seeder = new DevelopmentSeeder(Connections, Passwords);
        var prefix = "integration_" + Guid.NewGuid().ToString("N");
        Options = new DevelopmentSeedOptions([
            new(prefix + "_requester", "Test", "Requester", UserRole.Requester, Password),
            new(prefix + "_operator", "Test", "Operator", UserRole.Operator, Password),
            new(prefix + "_supervisor", "Test", "Supervisor", UserRole.Supervisor, Password),
            new(prefix + "_second", "Test", "Second", UserRole.Operator, Password),
            new(prefix + "_third", "Test", "Third", UserRole.Operator, Password)
        ], includeSamples);
    }

    public static async Task<SqlIntegrationContext> CreateAsync(bool includeSamples = false)
    {
        var context = new SqlIntegrationContext(includeSamples);
        context.SeedResult = await context.Seeder.SeedAsync("Development", context.Options);
        context.Requester = await context.FindAsync(context.Options.Users[0].UserName);
        context.Operator = await context.FindAsync(context.Options.Users[1].UserName);
        context.Supervisor = await context.FindAsync(context.Options.Users[2].UserName);
        context.SecondOperator = await context.FindAsync(context.Options.Users[3].UserName);
        context.ThirdOperator = await context.FindAsync(context.Options.Users[4].UserName);
        return context;
    }

    private async Task<AppUser> FindAsync(string userName) =>
        (await Users.GetCredentialsByUserNameAsync(userName))?.User
        ?? throw new InvalidOperationException("The integration fixture user was not created.");
}
