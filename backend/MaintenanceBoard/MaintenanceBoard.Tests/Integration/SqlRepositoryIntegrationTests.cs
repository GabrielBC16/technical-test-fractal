using Dapper;
using MaintenanceBoard.Application.Access;
using MaintenanceBoard.Application.Common.Exceptions;
using MaintenanceBoard.Application.Tickets;
using MaintenanceBoard.Application.Tickets.Exceptions;
using MaintenanceBoard.Domain.Tickets;
using MaintenanceBoard.Infrastructure.Development;
using Microsoft.Data.SqlClient;

namespace MaintenanceBoard.Tests.Integration;

[Collection("SQL Server integration")]
[Trait("Category", "Integration")]
public sealed class SqlRepositoryIntegrationTests
{
    [SqlServerFact]
    public async Task Ticket_lifecycle_round_trips_assignments_history_UTC_and_credentials()
    {
        var context = await SqlIntegrationContext.CreateAsync();
        const string title = "Reader '; DROP TABLE tickets.Ticket; --";
        var id = await context.Tickets.CreateAsync(title, "The café terminal is offline.", context.Requester.UserId);
        var ticket = await context.Tickets.GetByIdAsync(id);
        Assert.NotNull(ticket);
        Assert.Equal(title, ticket.Title);
        Assert.Equal(TicketStatus.Pending, ticket.Status);
        Assert.Equal(DateTimeKind.Utc, ticket.CreatedAtUtc.Kind);
        Assert.Empty(await context.Tickets.GetAssignmentsAsync(id));
        var initial = await context.Tickets.GetDetailsAsync(id);
        Assert.NotNull(initial);
        Assert.Equal(TicketEventType.Created, Assert.Single(initial.History).EventType);

        await context.Tickets.AssignAsync(id, context.Operator.UserId, context.Operator.UserId, null);
        await context.Tickets.AssignAsync(id, context.Supervisor.UserId, context.Operator.UserId, "Specialist supervision.");
        await context.Tickets.TransitionAsync(id, TicketStatus.Pending, TicketStatus.InProgress, context.Supervisor.UserId, null, null);
        await context.Tickets.AddCommentAsync(id, context.Requester.UserId, "The equipment is still offline.");
        await context.Tickets.TransitionAsync(id, TicketStatus.InProgress, TicketStatus.Closed, context.Operator.UserId, null, "Reader module failure.");

        var details = await context.Tickets.GetDetailsAsync(id);
        Assert.NotNull(details);
        Assert.Equal(TicketStatus.Closed, details.Ticket.Status);
        Assert.Equal("Reader module failure.", details.Cause);
        Assert.Equal(2, details.Ticket.Assignees.Count);
        Assert.Equal(6, details.History.Count);
        Assert.Equal(2, details.History.Count(item => item.EventType == TicketEventType.StatusChanged));
        Assert.Equal(details.History.OrderBy(item => item.CreatedAtUtc).ThenBy(item => item.HistoryId), details.History);
        Assert.All(details.History, item => Assert.Equal(DateTimeKind.Utc, item.CreatedAtUtc.Kind));
        Assert.Contains(details.History, item => item.EventType == TicketEventType.Assigned &&
            item.RelatedUserId == context.Supervisor.UserId && item.Comment == "Specialist supervision.");

        var login = await new AuthService(context.Users, context.Passwords).LoginAsync(
            new LoginRequest(context.Requester.UserName, context.Password));
        Assert.Equal(context.Requester.UserId, login.UserId);
        Assert.Null(await context.Users.GetCredentialsByUserNameAsync("missing' OR 1=1 --"));
        Assert.Null(await context.Tickets.GetByIdAsync(-1));
        Assert.Null(await context.Tickets.GetDetailsAsync(-1));
        var candidates = await context.Users.GetActiveAssignableUsersAsync();
        Assert.Contains(candidates, user => user.UserId == context.Supervisor.UserId);
        Assert.DoesNotContain(candidates, user => user.UserId == context.Requester.UserId);
    }

    [SqlServerFact]
    public async Task Board_and_date_filters_use_real_SQL_and_preserve_multiple_assignees()
    {
        var context = await SqlIntegrationContext.CreateAsync(includeSamples: true);
        var board = await context.Tickets.GetBoardAsync();
        Assert.DoesNotContain(board, item => item.Status == TicketStatus.Rejected);
        var own = (await context.Tickets.GetListAsync(new TicketQueryFilter(null, null, null)))
            .Where(item => item.ReportedByUserId == context.Requester.UserId).ToArray();
        Assert.Equal(4, own.Length);
        var pending = Assert.Single(own, item => item.Status == TicketStatus.Pending);
        Assert.Equal(2, pending.Assignees.Count);
        Assert.Equal(DateTimeKind.Utc, pending.Assignees[0].AssignedAtUtc.Kind);
        var rejected = Assert.Single(own, item => item.Status == TicketStatus.Rejected);
        var filtered = await context.Tickets.GetListAsync(new TicketQueryFilter(TicketStatus.Rejected, null, null));
        Assert.Contains(filtered, item => item.TicketId == rejected.TicketId);
        Assert.All(filtered, item => Assert.Equal(TicketStatus.Rejected, item.Status));

        var inclusive = await context.Tickets.GetListAsync(new TicketQueryFilter(null,
            pending.CreatedAtUtc, pending.CreatedAtUtc.AddTicks(1)));
        Assert.Contains(inclusive, item => item.TicketId == pending.TicketId);
        var exclusive = await context.Tickets.GetListAsync(new TicketQueryFilter(null, null, pending.CreatedAtUtc));
        Assert.DoesNotContain(exclusive, item => item.TicketId == pending.TicketId);
        var fullRange = await context.Tickets.GetListAsync(new TicketQueryFilter(null,
            DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc),
            DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc)));
        Assert.Contains(fullRange, item => item.TicketId == pending.TicketId);
    }

    [SqlServerFact]
    public async Task Expected_SQL_errors_are_translated_when_invoking_real_procedures()
    {
        var context = await SqlIntegrationContext.CreateAsync();
        var id = await context.Tickets.CreateAsync("Error translation validation", "A validation-only ticket.", context.Requester.UserId);
        await Assert.ThrowsAsync<TicketNotFoundException>(() =>
            context.Tickets.AssignAsync(-1, context.Operator.UserId, context.Operator.UserId, null));
        await Assert.ThrowsAsync<UnauthenticatedUserException>(() => context.Tickets.CreateAsync("Title", "Description", -1));
        await Assert.ThrowsAsync<RequestValidationException>(() => context.Tickets.CreateAsync(" ", "Description", context.Requester.UserId));
        await Assert.ThrowsAsync<ForbiddenOperationException>(() => context.Tickets.AssignAsync(id, context.Operator.UserId, context.Requester.UserId, null));
        await Assert.ThrowsAsync<InvalidTicketAssigneeException>(() => context.Tickets.AssignAsync(id, context.Requester.UserId, context.Operator.UserId, null));
        await Assert.ThrowsAsync<InvalidTicketTransitionException>(() => context.Tickets.TransitionAsync(
            id, TicketStatus.Pending, TicketStatus.Closed, context.Operator.UserId, null, "Cause."));
        await Assert.ThrowsAsync<TicketRejectionReasonRequiredException>(() => context.Tickets.TransitionAsync(
            id, TicketStatus.Pending, TicketStatus.Rejected, context.Operator.UserId, null, null));
        await Assert.ThrowsAsync<TicketCommentRequiredException>(() => context.Tickets.AddCommentAsync(id, context.Operator.UserId, " "));

        await context.Tickets.AssignAsync(id, context.Operator.UserId, context.Operator.UserId, null);
        await Assert.ThrowsAsync<TicketAssignmentConflictException>(() =>
            context.Tickets.AssignAsync(id, context.Operator.UserId, context.Operator.UserId, null));
        await Assert.ThrowsAsync<TicketAssignmentJustificationRequiredException>(() =>
            context.Tickets.AssignAsync(id, context.Supervisor.UserId, context.Operator.UserId, null));
        await context.Tickets.AssignAsync(id, context.Supervisor.UserId, context.Operator.UserId, "Supervision.");
        await context.Tickets.AssignAsync(id, context.SecondOperator.UserId, context.Operator.UserId, "Specialist review.");
        await Assert.ThrowsAsync<TicketAssignmentLimitException>(() =>
            context.Tickets.AssignAsync(id, context.ThirdOperator.UserId, context.Operator.UserId, "Fourth review."));
        await context.Tickets.TransitionAsync(id, TicketStatus.Pending, TicketStatus.InProgress, context.Operator.UserId, null, null);
        await Assert.ThrowsAsync<TicketConcurrencyException>(() => context.Tickets.TransitionAsync(
            id, TicketStatus.Pending, TicketStatus.Rejected, context.Operator.UserId, "Reason.", null));
        await Assert.ThrowsAsync<TicketCauseRequiredException>(() => context.Tickets.TransitionAsync(
            id, TicketStatus.InProgress, TicketStatus.Closed, context.Operator.UserId, null, " \t\u202F "));
        await Assert.ThrowsAsync<ImmutableTicketHistoryException>(() => context.Database.ExecuteAsync(async (connection, token) =>
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE tickets.TicketHistory SET Comment = N'Edited' WHERE TicketId = @TicketId;",
                new { TicketId = id }, cancellationToken: token));
        }));
    }

    [SqlServerFact]
    public async Task Concurrent_assignments_cannot_exceed_three_or_duplicate_history()
    {
        var context = await SqlIntegrationContext.CreateAsync();
        var id = await context.Tickets.CreateAsync("Assignment race", "A validation-only race.", context.Requester.UserId);
        await context.Tickets.AssignAsync(id, context.Operator.UserId, context.Operator.UserId, null);
        await context.Tickets.AssignAsync(id, context.Supervisor.UserId, context.Operator.UserId, "Supervision.");

        var results = await Task.WhenAll(
            CaptureAsync(() => context.Tickets.AssignAsync(id, context.SecondOperator.UserId, context.Operator.UserId, "Specialist.")),
            CaptureAsync(() => context.Tickets.AssignAsync(id, context.ThirdOperator.UserId, context.Operator.UserId, "Specialist.")));

        Assert.Single(results, result => result is null);
        Assert.IsType<TicketAssignmentLimitException>(Assert.Single(results, result => result is not null));
        Assert.Equal(3, (await context.Tickets.GetAssignmentsAsync(id)).Count);
        var details = await context.Tickets.GetDetailsAsync(id);
        Assert.NotNull(details);
        Assert.Equal(3, details.History.Count(item => item.EventType == TicketEventType.Assigned));
    }

    [SqlServerFact]
    public async Task Concurrent_transitions_produce_one_event_and_one_concurrency_failure()
    {
        var context = await SqlIntegrationContext.CreateAsync();
        var id = await context.Tickets.CreateAsync("Transition race", "A validation-only race.", context.Requester.UserId);
        await context.Tickets.AssignAsync(id, context.Operator.UserId, context.Operator.UserId, null);

        var results = await Task.WhenAll(
            CaptureAsync(() => context.Tickets.TransitionAsync(id, TicketStatus.Pending, TicketStatus.InProgress, context.Operator.UserId, null, null)),
            CaptureAsync(() => context.Tickets.TransitionAsync(id, TicketStatus.Pending, TicketStatus.InProgress, context.Supervisor.UserId, null, null)));

        Assert.Single(results, result => result is null);
        Assert.IsType<TicketConcurrencyException>(Assert.Single(results, result => result is not null));
        var details = await context.Tickets.GetDetailsAsync(id);
        Assert.NotNull(details);
        Assert.Equal(TicketStatus.InProgress, details.Ticket.Status);
        Assert.Single(details.History, item => item.EventType == TicketEventType.StatusChanged);
    }

    [SqlServerFact]
    public async Task Seeder_is_repeatable_and_does_not_replace_existing_passwords()
    {
        var context = await SqlIntegrationContext.CreateAsync(includeSamples: true);
        Assert.Equal(5, context.SeedResult.CreatedUsers);
        Assert.Equal(4, context.SeedResult.CreatedTickets);
        var before = await context.Users.GetCredentialsByUserNameAsync(context.Requester.UserName);
        Assert.NotNull(before);
        var modified = context.Options with
        {
            Users = context.Options.Users.Select(user => user with { Password = Guid.NewGuid().ToString("N") }).ToArray()
        };

        var repeated = await context.Seeder.SeedAsync("Development", modified);
        var after = await context.Users.GetCredentialsByUserNameAsync(context.Requester.UserName);

        Assert.Equal(new DevelopmentSeedResult(0, 0), repeated);
        Assert.NotNull(after);
        Assert.Equal(before.PasswordHash, after.PasswordHash);
        Assert.True(context.Passwords.Verify(context.Password, after.PasswordHash));
    }

    [SqlServerFact]
    public async Task SQL_whitespace_guards_match_the_domain_for_all_Unicode_whitespace_characters()
    {
        var context = await SqlIntegrationContext.CreateAsync();
        int[] codePoints = [9, 10, 11, 12, 13, 32, 133, 160, 5760, 8192, 8193, 8194,
            8195, 8196, 8197, 8198, 8199, 8200, 8201, 8202, 8232, 8233, 8239, 8287, 12288];
        await using var connection = await context.Connections.OpenAsync();
        foreach (var codePoint in codePoints)
        {
            var value = char.ConvertFromUtf32(codePoint);
            Assert.True(string.IsNullOrWhiteSpace(value));
            Assert.False(await connection.QuerySingleAsync<bool>(
                "SELECT tickets.ufn_HasText(@Value);", new { Value = value }));
        }

        Assert.True(await connection.QuerySingleAsync<bool>(
            "SELECT tickets.ufn_HasText(@Value);", new { Value = " \tPower supply failure.\u202F " }));
    }

    [SqlServerFact]
    public async Task A_failed_seed_does_not_leave_partially_created_users()
    {
        var context = await SqlIntegrationContext.CreateAsync();
        var freshUser = context.Options.Users[0] with { UserName = "rollback_" + Guid.NewGuid().ToString("N") };
        var incompatibleExistingUser = context.Options.Users[1] with { Role = MaintenanceBoard.Domain.Access.UserRole.Supervisor };
        var options = new DevelopmentSeedOptions([freshUser, incompatibleExistingUser], IncludeSampleTickets: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.Seeder.SeedAsync("Development", options));

        Assert.Null(await context.Users.GetCredentialsByUserNameAsync(freshUser.UserName));
    }

    [SqlServerFact]
    public async Task Failed_history_insertion_rolls_back_the_status_and_reaches_the_caller_as_an_operational_error()
    {
        var context = await SqlIntegrationContext.CreateAsync();
        var id = await context.Tickets.CreateAsync("History failure", "A validation-only rollback fixture.", context.Requester.UserId);
        await context.Tickets.AssignAsync(id, context.Operator.UserId, context.Operator.UserId, null);
        await context.Tickets.TransitionAsync(id, TicketStatus.Pending, TicketStatus.InProgress, context.Operator.UserId, null, null);
        var before = await context.Tickets.GetDetailsAsync(id);
        Assert.NotNull(before);
        await using var setup = await context.Connections.OpenAsync();
        const string createTrigger = """
            CREATE OR ALTER TRIGGER tickets.trg_Validation_InfrastructureHistoryFailure
            ON tickets.TicketHistory AFTER INSERT AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (SELECT 1 FROM inserted WHERE Comment = N'InfrastructureForcedHistoryFailure')
                    THROW 51090, N'Simulated history storage failure.', 1;
            END;
            """;
        await setup.ExecuteAsync(createTrigger);
        try
        {
            var exception = await Assert.ThrowsAsync<SqlException>(() => context.Tickets.TransitionAsync(
                id, TicketStatus.InProgress, TicketStatus.Closed, context.Operator.UserId,
                "InfrastructureForcedHistoryFailure", "Power supply failure."));
            Assert.Equal(51090, exception.Number);
            var after = await context.Tickets.GetDetailsAsync(id);
            Assert.NotNull(after);
            Assert.Equal(TicketStatus.InProgress, after.Ticket.Status);
            Assert.Null(after.Cause);
            Assert.Equal(before.History.Count, after.History.Count);
        }
        finally
        {
            await setup.ExecuteAsync("DROP TRIGGER tickets.trg_Validation_InfrastructureHistoryFailure;");
        }
    }

    private static async Task<Exception?> CaptureAsync(Func<Task> action)
    {
        try
        {
            await action();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }
}
