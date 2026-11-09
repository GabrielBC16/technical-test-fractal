namespace MaintenanceBoard.Domain.Tickets;

// A persisted ticket snapshot. Application services coordinate changes through repositories.
public sealed record Ticket(
    int TicketId,
    string Title,
    string Description,
    TicketStatus Status,
    string? Cause,
    int ReportedByUserId,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
