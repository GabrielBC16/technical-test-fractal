namespace MaintenanceBoard.Application.Tickets;

// Actor identities intentionally do not appear in request models.
public sealed record CreateTicketRequest(string? Title, string? Description);

public sealed record TransitionTicketRequest(
    string? ExpectedStatus,
    string? NewStatus,
    string? Comment = null,
    string? Cause = null);

public sealed record AssignTicketRequest(int AssignedUserId, string? Justification = null);

public sealed record AddTicketCommentRequest(string? Comment);

public sealed record TicketListRequest(
    string? Status = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null);
