using MaintenanceBoard.Application.Access;
using MaintenanceBoard.Application.Tickets;
using MaintenanceBoard.Infrastructure.Access;
using MaintenanceBoard.Infrastructure.Development;
using MaintenanceBoard.Infrastructure.Persistence;
using MaintenanceBoard.Infrastructure.Tickets;
using Microsoft.Extensions.DependencyInjection;

namespace MaintenanceBoard.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddSingleton<ISqlConnectionFactory>(new SqlConnectionFactory(connectionString));
        services.AddScoped<SqlExecutor>();
        services.AddScoped<ITicketRepository, SqlTicketRepository>();
        services.AddScoped<IUserRepository, SqlUserRepository>();
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<DevelopmentSeeder>();
        return services;
    }
}
