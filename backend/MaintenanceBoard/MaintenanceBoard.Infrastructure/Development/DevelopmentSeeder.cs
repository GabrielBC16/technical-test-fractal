using System.Data;
using System.Text;
using Dapper;
using MaintenanceBoard.Application.Access;
using MaintenanceBoard.Domain.Access;
using MaintenanceBoard.Domain.Tickets;
using MaintenanceBoard.Infrastructure.Access;
using MaintenanceBoard.Infrastructure.Persistence;

namespace MaintenanceBoard.Infrastructure.Development;

public sealed class DevelopmentSeeder(ISqlConnectionFactory connections, IPasswordHasher passwords)
{
    public Task<DevelopmentSeedResult> SeedAsync(
        string environmentName, CancellationToken cancellationToken = default) =>
        SeedAsync(environmentName, DefaultDevelopmentData.CreateOptions(), cancellationToken);

    public async Task<DevelopmentSeedResult> SeedAsync(
        string environmentName, DevelopmentSeedOptions options, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Development seeding is disabled outside the Development environment.");
        }

        Validate(options);
        var database = new SqlExecutor(connections);
        return await database.ExecuteAsync(async (connection, token) =>
        {
            await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, token);
            const string acquireLock = """
                DECLARE @Result int;
                EXEC @Result = sys.sp_getapplock @Resource=N'MaintenanceBoardDevelopmentSeed',
                    @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=15000;
                SELECT @Result;
                """;
            var lockResult = await connection.QuerySingleAsync<int>(
                new CommandDefinition(acquireLock, transaction: transaction, cancellationToken: token));
            if (lockResult < 0)
            {
                throw new InvalidOperationException("Could not acquire the development seed lock.");
            }

            var users = new List<AppUser>();
            var createdUsers = 0;
            foreach (var user in options.Users)
            {
                const string findUser = """
                    SELECT UserId, UserName, Names, LastNames, Role, IsActive, CreatedAtUtc
                    FROM access.AppUser WITH (UPDLOCK, HOLDLOCK)
                    WHERE UserName = @UserName;
                    """;
                var existing = await connection.QuerySingleOrDefaultAsync<AppUserRow>(new CommandDefinition(
                    findUser, new { UserName = user.UserName.Trim() }, transaction, cancellationToken: token));
                if (existing is not null)
                {
                    var persisted = existing.ToUser();
                    if (!persisted.IsActive || persisted.Role != user.Role)
                    {
                        throw new InvalidOperationException("An existing development user has an incompatible role or is inactive.");
                    }

                    // Preserve existing passwords and user data; this is not a user-management endpoint.
                    users.Add(persisted);
                    continue;
                }

                const string insertUser = """
                    INSERT INTO access.AppUser (UserName, PasswordHash, Names, LastNames, Role, IsActive)
                    OUTPUT inserted.UserId, inserted.UserName, inserted.Names, inserted.LastNames,
                           inserted.Role, inserted.IsActive, inserted.CreatedAtUtc
                    VALUES (@UserName, @PasswordHash, @Names, @LastNames, @Role, 1);
                    """;
                var created = await connection.QuerySingleAsync<AppUserRow>(new CommandDefinition(insertUser, new
                {
                    UserName = user.UserName.Trim(),
                    PasswordHash = passwords.Hash(user.Password),
                    Names = user.Names.Trim(),
                    LastNames = user.LastNames.Trim(),
                    Role = user.Role.ToString()
                }, transaction, cancellationToken: token));
                users.Add(created.ToUser());
                createdUsers++;
            }

            var createdTickets = 0;
            if (options.IncludeSampleTickets)
            {
                var requester = users.First(user => user.Role == UserRole.Requester);
                var primary = users.First(user => user.Role is UserRole.Operator or UserRole.Supervisor);
                var secondary = users.FirstOrDefault(user => user.UserId != primary.UserId &&
                    user.Role is UserRole.Operator or UserRole.Supervisor);

                foreach (var (title, description, target) in Samples)
                {
                    const string findTicket = """
                        SELECT TOP (1) TicketId FROM tickets.Ticket WITH (UPDLOCK, HOLDLOCK)
                        WHERE ReportedByUserId = @ReportedByUserId AND Title = @Title
                        ORDER BY TicketId;
                        """;
                    var existingId = await connection.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
                        findTicket, new { ReportedByUserId = requester.UserId, Title = title }, transaction, cancellationToken: token));
                    if (existingId is not null)
                    {
                        continue;
                    }

                    var ticketId = await connection.QuerySingleAsync<int>(new CommandDefinition(
                        "tickets.usp_CreateTicket", new { Title = title, Description = description, ReportedByUserId = requester.UserId },
                        transaction, commandType: CommandType.StoredProcedure, cancellationToken: token));

                    if (target != TicketStatus.Rejected)
                    {
                        await connection.ExecuteAsync(new CommandDefinition("tickets.usp_AssignTicket", new
                        {
                            TicketId = ticketId, AssignedUserId = primary.UserId, ActorUserId = primary.UserId,
                            Justification = (string?)null
                        }, transaction, commandType: CommandType.StoredProcedure, cancellationToken: token));

                        if (secondary is not null && target == TicketStatus.Pending)
                        {
                            await connection.ExecuteAsync(new CommandDefinition("tickets.usp_AssignTicket", new
                            {
                                TicketId = ticketId, AssignedUserId = secondary.UserId, ActorUserId = primary.UserId,
                                Justification = "Se requiere apoyo de especialistas en electricidad y hardware."
                            }, transaction, commandType: CommandType.StoredProcedure, cancellationToken: token));
                        }
                    }

                    if (target is TicketStatus.InProgress or TicketStatus.Closed)
                    {
                        await TransitionAsync(connection, transaction, ticketId, TicketStatus.Pending,
                            TicketStatus.InProgress, primary.UserId, null, null, token);
                    }

                    if (target == TicketStatus.Closed)
                    {
                        await TransitionAsync(connection, transaction, ticketId, TicketStatus.InProgress,
                            TicketStatus.Closed, primary.UserId, null, "Desgaste del módulo lector de tarjetas.", token);
                    }
                    else if (target == TicketStatus.Rejected)
                    {
                        await TransitionAsync(connection, transaction, ticketId, TicketStatus.Pending,
                            TicketStatus.Rejected, primary.UserId, "El incidente ya está registrado en otro ticket.", null, token);
                    }

                    await connection.ExecuteAsync(new CommandDefinition("tickets.usp_AddTicketComment", new
                    {
                        TicketId = ticketId, ActorUserId = requester.UserId, Comment = "Ticket de demostración de desarrollo."
                    }, transaction, commandType: CommandType.StoredProcedure, cancellationToken: token));
                    createdTickets++;
                }
            }

            await transaction.CommitAsync(token);
            return new DevelopmentSeedResult(createdUsers, createdTickets);
        }, cancellationToken);
    }

    private static Task<int> TransitionAsync(
        Microsoft.Data.SqlClient.SqlConnection connection, System.Data.Common.DbTransaction transaction,
        int ticketId, TicketStatus expectedStatus, TicketStatus newStatus, int actorUserId,
        string? comment, string? cause, CancellationToken cancellationToken) =>
        connection.ExecuteAsync(new CommandDefinition("tickets.usp_TransitionTicketStatus", new
        {
            TicketId = ticketId, ExpectedStatus = expectedStatus.ToString(), NewStatus = newStatus.ToString(),
            ActorUserId = actorUserId, Comment = comment, Cause = cause
        }, transaction, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));

    private static void Validate(DevelopmentSeedOptions options)
    {
        if (options.Users.Count == 0)
        {
            throw new InvalidOperationException("Configure at least one development seed user.");
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var user in options.Users)
        {
            if (string.IsNullOrWhiteSpace(user.UserName) || user.UserName.Length > 100 ||
                string.IsNullOrWhiteSpace(user.Names) || user.Names.Length > 100 ||
                string.IsNullOrWhiteSpace(user.LastNames) || user.LastNames.Length > 150 ||
                !Enum.IsDefined(user.Role) || !names.Add(user.UserName.Trim()))
            {
                throw new InvalidOperationException("Development seed users must have unique names, valid roles, and valid field lengths.");
            }

            if (string.IsNullOrWhiteSpace(user.Password) || Encoding.UTF8.GetByteCount(user.Password) > 72)
            {
                throw new InvalidOperationException("Development passwords must contain text and fit within 72 UTF-8 bytes.");
            }
        }

        if (options.IncludeSampleTickets &&
            (!options.Users.Any(user => user.Role == UserRole.Requester) ||
             !options.Users.Any(user => user.Role is UserRole.Operator or UserRole.Supervisor)))
        {
            throw new InvalidOperationException("Sample tickets require a requester and at least one operator or supervisor.");
        }
    }

    private static readonly (string Title, string Description, TicketStatus Status)[] Samples =
    [
        ("Cajero ATM malogrado", "La pantalla de la consola se quedó congelada.", TicketStatus.Pending),
        ("Alarma UPS", "El sistema de respaldo eléctrico reporta una falla.", TicketStatus.InProgress),
        ("Lector de tarjetas averiado", "El lector de tarjetas falla al leer.", TicketStatus.Closed),
        ("Incidente duplicado del cajero ATM", "La falla del cajero ya está registrada en otro ticket.", TicketStatus.Rejected)
    ];
}
