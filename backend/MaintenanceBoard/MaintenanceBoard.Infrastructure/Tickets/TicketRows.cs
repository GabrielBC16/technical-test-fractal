using MaintenanceBoard.Application.Tickets;
using MaintenanceBoard.Domain.Tickets;
using MaintenanceBoard.Infrastructure.Persistence;

namespace MaintenanceBoard.Infrastructure.Tickets;

internal sealed class TicketRow
{
    public int TicketId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Status { get; set; } = "";
    public string? Cause { get; set; }
    public int ReportedByUserId { get; set; }
    public string ReportedByDisplayName { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public Ticket ToTicket() => new(TicketId, Title, Description, SqlValues.ParseEnum<TicketStatus>(Status),
        Cause, ReportedByUserId, SqlValues.AsUtc(CreatedAtUtc), SqlValues.AsUtc(UpdatedAtUtc));

    public TicketListItem ToListItem(IReadOnlyList<TicketAssignee> assignees) =>
        new(TicketId, Title, Description, SqlValues.ParseEnum<TicketStatus>(Status), ReportedByUserId,
            ReportedByDisplayName, SqlValues.AsUtc(CreatedAtUtc), SqlValues.AsUtc(UpdatedAtUtc), assignees);
}

internal sealed class TicketAssignmentRow
{
    public int TicketId { get; set; }
    public int AssignedUserId { get; set; }
    public int AssignedByUserId { get; set; }
    public string? Justification { get; set; }
    public DateTime AssignedAtUtc { get; set; }
    public string Names { get; set; } = "";
    public string LastNames { get; set; } = "";

    public TicketAssignment ToAssignment() =>
        new(TicketId, AssignedUserId, AssignedByUserId, Justification, SqlValues.AsUtc(AssignedAtUtc));

    public TicketAssignee ToAssignee() =>
        new(AssignedUserId, Names, LastNames, AssignedByUserId, Justification, SqlValues.AsUtc(AssignedAtUtc));
}

internal sealed class TicketHistoryRow
{
    public long HistoryId { get; set; }
    public int ActorUserId { get; set; }
    public string ActorDisplayName { get; set; } = "";
    public int? RelatedUserId { get; set; }
    public string? RelatedUserDisplayName { get; set; }
    public string EventType { get; set; } = "";
    public string? PreviousStatus { get; set; }
    public string? NewStatus { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public TicketHistoryItem ToHistoryItem() => new(
        HistoryId, ActorUserId, ActorDisplayName, RelatedUserId, RelatedUserDisplayName,
        SqlValues.ParseEnum<TicketEventType>(EventType),
        PreviousStatus is null ? null : SqlValues.ParseEnum<TicketStatus>(PreviousStatus),
        NewStatus is null ? null : SqlValues.ParseEnum<TicketStatus>(NewStatus),
        Comment, SqlValues.AsUtc(CreatedAtUtc));
}
