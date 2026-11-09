namespace MaintenanceBoard.Domain.Tickets;

public sealed record TicketHistory(
    long HistoryId,
    int TicketId,
    int ActorUserId,
    int? RelatedUserId,
    TicketEventType EventType,
    TicketStatus? PreviousStatus,
    TicketStatus? NewStatus,
    string? Comment,
    DateTime CreatedAtUtc);
