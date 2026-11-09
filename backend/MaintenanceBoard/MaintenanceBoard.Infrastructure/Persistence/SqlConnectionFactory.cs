using Microsoft.Data.SqlClient;

namespace MaintenanceBoard.Infrastructure.Persistence;

public sealed class SqlConnectionFactory : ISqlConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Configure the MaintenanceBoard database connection string.");
        }

        var settings = new SqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(settings.DataSource) || string.IsNullOrWhiteSpace(settings.InitialCatalog))
        {
            throw new InvalidOperationException("The database connection must specify a server and an application database.");
        }

        _connectionString = settings.ConnectionString;
    }

    public async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqlConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
