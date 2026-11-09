using Microsoft.Data.SqlClient;

namespace MaintenanceBoard.Infrastructure.Persistence;

public interface ISqlConnectionFactory
{
    Task<SqlConnection> OpenAsync(CancellationToken cancellationToken = default);
}
