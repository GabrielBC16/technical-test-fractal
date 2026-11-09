using System.Data;
using Dapper;
using MaintenanceBoard.Application.Tickets;
using MaintenanceBoard.Domain.Tickets;
using MaintenanceBoard.Infrastructure.Persistence;

namespace MaintenanceBoard.Infrastructure.Tickets;

public sealed class SqlTicketRepository(SqlExecutor database) : ITicketRepository
{
    public Task<Ticket?> GetByIdAsync(int ticketId, CancellationToken cancellationToken = default) =>
        database.ExecuteAsync(async (connection, token) =>
        {
            var row = await connection.QuerySingleOrDefaultAsync<TicketRow>(
                new CommandDefinition(TicketQueries.ById, new { TicketId = ticketId }, cancellationToken: token));
            return row?.ToTicket();
        }, cancellationToken);

    public Task<IReadOnlyList<TicketAssignment>> GetAssignmentsAsync(int ticketId, CancellationToken cancellationToken = default) =>
        database.ExecuteAsync<IReadOnlyList<TicketAssignment>>(async (connection, token) =>
        {
            var rows = await connection.QueryAsync<TicketAssignmentRow>(
                new CommandDefinition(TicketQueries.Assignments, new { TicketId = ticketId }, cancellationToken: token));
            return rows.Select(row => row.ToAssignment()).ToArray();
        }, cancellationToken);

    public Task<TicketDetails?> GetDetailsAsync(int ticketId, CancellationToken cancellationToken = default) =>
        database.ExecuteAsync(async (connection, token) =>
        {
            // Keep the ticket, assignments, and history coherent across the three result sets.
            await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, token);
            using var results = await connection.QueryMultipleAsync(new CommandDefinition(
                TicketQueries.Details, new { TicketId = ticketId }, transaction, cancellationToken: token));
            var ticket = await results.ReadSingleOrDefaultAsync<TicketRow>();
            var assignments = (await results.ReadAsync<TicketAssignmentRow>()).ToArray();
            var history = (await results.ReadAsync<TicketHistoryRow>()).ToArray();
            await transaction.CommitAsync(token);

            return ticket is null ? null : new TicketDetails(
                ticket.ToListItem(assignments.Select(row => row.ToAssignee()).ToArray()),
                ticket.Cause, history.Select(row => row.ToHistoryItem()).ToArray());
        }, cancellationToken);

    public Task<IReadOnlyList<TicketListItem>> GetBoardAsync(CancellationToken cancellationToken = default) =>
        ReadListAsync(new TicketQueryFilter(null, null, null), includeRejected: false, cancellationToken);

    public Task<IReadOnlyList<TicketListItem>> GetListAsync(TicketQueryFilter filter, CancellationToken cancellationToken = default) =>
        ReadListAsync(filter, includeRejected: true, cancellationToken);

    public Task<int> CreateAsync(string title, string description, int reportedByUserId, CancellationToken cancellationToken = default) =>
        database.ExecuteAsync((connection, token) => connection.QuerySingleAsync<int>(new CommandDefinition(
            "tickets.usp_CreateTicket", new { Title = title, Description = description, ReportedByUserId = reportedByUserId },
            commandType: CommandType.StoredProcedure, cancellationToken: token)), cancellationToken);

    public Task TransitionAsync(int ticketId, TicketStatus expectedStatus, TicketStatus newStatus, int actorUserId,
        string? comment, string? cause, CancellationToken cancellationToken = default) =>
        database.ExecuteAsync(async (connection, token) =>
        {
            await connection.ExecuteAsync(new CommandDefinition("tickets.usp_TransitionTicketStatus", new
            {
                TicketId = ticketId,
                ExpectedStatus = expectedStatus.ToString(),
                NewStatus = newStatus.ToString(),
                ActorUserId = actorUserId,
                Comment = comment,
                Cause = cause
            }, commandType: CommandType.StoredProcedure, cancellationToken: token));
        }, cancellationToken);

    public Task AssignAsync(int ticketId, int assignedUserId, int actorUserId, string? justification, CancellationToken cancellationToken = default) =>
        database.ExecuteAsync(async (connection, token) =>
        {
            await connection.ExecuteAsync(new CommandDefinition("tickets.usp_AssignTicket", new
            {
                TicketId = ticketId,
                AssignedUserId = assignedUserId,
                ActorUserId = actorUserId,
                Justification = justification
            }, commandType: CommandType.StoredProcedure, cancellationToken: token));
        }, cancellationToken);

    public Task AddCommentAsync(int ticketId, int actorUserId, string comment, CancellationToken cancellationToken = default) =>
        database.ExecuteAsync(async (connection, token) =>
        {
            await connection.ExecuteAsync(new CommandDefinition("tickets.usp_AddTicketComment",
                new { TicketId = ticketId, ActorUserId = actorUserId, Comment = comment },
                commandType: CommandType.StoredProcedure, cancellationToken: token));
        }, cancellationToken);

    private Task<IReadOnlyList<TicketListItem>> ReadListAsync(
        TicketQueryFilter filter, bool includeRejected, CancellationToken cancellationToken) =>
        database.ExecuteAsync<IReadOnlyList<TicketListItem>>(async (connection, token) =>
        {
            var parameters = new DynamicParameters();
            parameters.Add("IncludeRejected", includeRejected, DbType.Boolean);
            parameters.Add("Status", filter.Status?.ToString(), DbType.String, size: 20);
            parameters.Add("FromUtc", filter.CreatedFromUtc, DbType.DateTime2);
            parameters.Add("ToUtc", filter.CreatedToUtc, DbType.DateTime2);

            await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, token);
            using var results = await connection.QueryMultipleAsync(
                new CommandDefinition(TicketQueries.List, parameters, transaction, cancellationToken: token));
            var tickets = (await results.ReadAsync<TicketRow>()).ToArray();
            var assignments = (await results.ReadAsync<TicketAssignmentRow>()).ToArray();
            await transaction.CommitAsync(token);

            var byTicket = assignments.GroupBy(row => row.TicketId).ToDictionary(
                group => group.Key, group => group.Select(row => row.ToAssignee()).ToArray());

            return tickets.Select(row => row.ToListItem(byTicket.GetValueOrDefault(row.TicketId) ?? [])).ToArray();
        }, cancellationToken);
}
