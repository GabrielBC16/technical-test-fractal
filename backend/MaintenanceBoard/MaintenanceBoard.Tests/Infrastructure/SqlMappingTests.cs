using System.Data;
using MaintenanceBoard.Domain.Tickets;
using MaintenanceBoard.Infrastructure.Persistence;
using MaintenanceBoard.Infrastructure.Tickets;

namespace MaintenanceBoard.Tests.Infrastructure;

public sealed class SqlMappingTests
{
    [Fact]
    public void SQL_datetime2_values_are_marked_UTC_without_shifting_the_clock()
    {
        var stored = new DateTime(2026, 11, 8, 9, 30, 0, DateTimeKind.Unspecified);
        var row = new TicketRow
        {
            TicketId = 1, Title = "Title", Description = "Description", Status = "Pending",
            CreatedAtUtc = stored, UpdatedAtUtc = stored
        };

        var result = row.ToTicket();

        Assert.Equal(DateTimeKind.Utc, result.CreatedAtUtc.Kind);
        Assert.Equal(stored.Ticks, result.CreatedAtUtc.Ticks);
        Assert.Equal(DateTimeKind.Utc, result.UpdatedAtUtc.Kind);
        Assert.Equal(TicketStatus.Pending, result.Status);
    }

    [Theory]
    [InlineData("Unknown")]
    [InlineData("pending")]
    [InlineData("Pending ")]
    [InlineData("0")]
    public void Unexpected_persisted_statuses_are_not_silently_coerced(string value)
    {
        Assert.Throws<DataException>(() => SqlValues.ParseEnum<TicketStatus>(value));
    }

    [Fact]
    public void Assignment_and_history_dates_follow_the_same_UTC_contract()
    {
        var assignment = new TicketAssignmentRow { AssignedAtUtc = new DateTime(2026, 11, 8) };
        var history = new TicketHistoryRow { EventType = "Comment", CreatedAtUtc = new DateTime(2026, 11, 8) };

        Assert.Equal(DateTimeKind.Utc, assignment.ToAssignment().AssignedAtUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, assignment.ToAssignee().AssignedAtUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, history.ToHistoryItem().CreatedAtUtc.Kind);
        Assert.Null(history.ToHistoryItem().NewStatus);
    }
}
