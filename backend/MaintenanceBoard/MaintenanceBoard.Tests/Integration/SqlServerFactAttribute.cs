namespace MaintenanceBoard.Tests.Integration;

[AttributeUsage(AttributeTargets.Method)]
public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MAINTENANCEBOARD_TEST_CONNECTION_STRING")))
        {
            Skip = "Set MAINTENANCEBOARD_TEST_CONNECTION_STRING to an isolated validation database.";
        }
    }
}

[CollectionDefinition("SQL Server integration", DisableParallelization = true)]
public sealed class SqlServerIntegrationCollection;
