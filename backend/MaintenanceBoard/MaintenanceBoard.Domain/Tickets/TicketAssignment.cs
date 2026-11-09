namespace MaintenanceBoard.Domain.Tickets;

public sealed record TicketAssignment(
    int TicketId,
    int AssignedUserId,
    int AssignedByUserId,
    string? Justification,
    DateTime AssignedAtUtc);
