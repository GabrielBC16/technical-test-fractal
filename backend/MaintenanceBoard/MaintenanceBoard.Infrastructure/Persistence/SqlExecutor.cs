using Microsoft.Data.SqlClient;

namespace MaintenanceBoard.Infrastructure.Persistence;

public sealed class SqlExecutor(ISqlConnectionFactory connections)
{
    public async Task<T> ExecuteAsync<T>(
        Func<SqlConnection, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await connections.OpenAsync(cancellationToken);
            return await operation(connection, cancellationToken);
        }
        catch (SqlException exception)
        {
            var translated = SqlErrorTranslator.Translate(exception);
            if (translated is not null)
            {
                throw translated;
            }

            throw;
        }
    }

    public Task ExecuteAsync(
        Func<SqlConnection, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(async (connection, token) =>
        {
            await operation(connection, token);
            return 0;
        }, cancellationToken);
}
