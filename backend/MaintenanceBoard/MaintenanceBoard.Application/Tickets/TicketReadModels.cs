using MaintenanceBoard.Domain.Tickets;

namespace MaintenanceBoard.Application.Tickets;

public sealed record TicketAssignee(
    int UserId,
    string Names,
    string LastNames,
    int AssignedByUserId,
    string? Justification,
    DateTime AssignedAtUtc);

public sealed record TicketListItem(
    int TicketId,
    string Title,
    string Description,
    TicketStatus Status,
    int ReportedByUserId,
    string ReportedByDisplayName,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyList<TicketAssignee> Assignees);

public sealed record TicketHistoryItem(
    long HistoryId,
    int ActorUserId,
    string ActorDisplayName,
    int? RelatedUserId,
    string? RelatedUserDisplayName,
    TicketEventType EventType,
    TicketStatus? PreviousStatus,
    TicketStatus? NewStatus,
    string? Comment,
    DateTime CreatedAtUtc);

public sealed record TicketDetails(
    TicketListItem Ticket,
    string? Cause,
    IReadOnlyList<TicketHistoryItem> History);

public sealed record TicketBoard(
    IReadOnlyList<TicketListItem> Pending,
    IReadOnlyList<TicketListItem> InProgress,
    IReadOnlyList<TicketListItem> Closed);

// UTC boundaries: From is inclusive and To is exclusive.
public sealed record TicketQueryFilter(
    TicketStatus? Status,
    DateTime? CreatedFromUtc,
    DateTime? CreatedToUtc);
