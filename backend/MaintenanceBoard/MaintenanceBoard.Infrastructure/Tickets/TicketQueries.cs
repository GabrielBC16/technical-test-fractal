namespace MaintenanceBoard.Infrastructure.Tickets;

internal static class TicketQueries
{
    public const string ById = """
        SELECT t.TicketId, t.Title, t.Description, t.Status, t.Cause, t.ReportedByUserId,
               t.CreatedAtUtc, t.UpdatedAtUtc
        FROM tickets.Ticket AS t
        WHERE t.TicketId = @TicketId;
        """;

    public const string Assignments = """
        SELECT TicketId, AssignedUserId, AssignedByUserId, Justification, AssignedAtUtc
        FROM tickets.TicketAssignment
        WHERE TicketId = @TicketId
        ORDER BY AssignedAtUtc, AssignedUserId;
        """;

    public const string List = """
        DECLARE @SelectedTickets TABLE (TicketId int PRIMARY KEY);
        INSERT INTO @SelectedTickets (TicketId)
        SELECT t.TicketId
        FROM tickets.Ticket AS t
        WHERE (@IncludeRejected = 1 OR t.Status IN (N'Pending', N'InProgress', N'Closed'))
          AND (@Status IS NULL OR t.Status = @Status)
          AND (@FromUtc IS NULL OR t.CreatedAtUtc >= @FromUtc)
          AND (@ToUtc IS NULL OR t.CreatedAtUtc < @ToUtc);

        SELECT t.TicketId, t.Title, t.Description, t.Status, t.Cause, t.ReportedByUserId,
               CONCAT(u.Names, N' ', u.LastNames) AS ReportedByDisplayName,
               t.CreatedAtUtc, t.UpdatedAtUtc
        FROM tickets.Ticket AS t
        INNER JOIN @SelectedTickets AS selected ON selected.TicketId = t.TicketId
        INNER JOIN access.AppUser AS u ON u.UserId = t.ReportedByUserId
        ORDER BY t.CreatedAtUtc DESC, t.TicketId DESC;

        SELECT a.TicketId, a.AssignedUserId, a.AssignedByUserId, a.Justification, a.AssignedAtUtc,
               u.Names, u.LastNames
        FROM tickets.TicketAssignment AS a
        INNER JOIN @SelectedTickets AS selected ON selected.TicketId = a.TicketId
        INNER JOIN access.AppUser AS u ON u.UserId = a.AssignedUserId
        ORDER BY a.TicketId, a.AssignedAtUtc, a.AssignedUserId;
        """;

    public const string Details = """
        SELECT t.TicketId, t.Title, t.Description, t.Status, t.Cause, t.ReportedByUserId,
               CONCAT(u.Names, N' ', u.LastNames) AS ReportedByDisplayName,
               t.CreatedAtUtc, t.UpdatedAtUtc
        FROM tickets.Ticket AS t
        INNER JOIN access.AppUser AS u ON u.UserId = t.ReportedByUserId
        WHERE t.TicketId = @TicketId;

        SELECT a.TicketId, a.AssignedUserId, a.AssignedByUserId, a.Justification, a.AssignedAtUtc,
               u.Names, u.LastNames
        FROM tickets.TicketAssignment AS a
        INNER JOIN access.AppUser AS u ON u.UserId = a.AssignedUserId
        WHERE a.TicketId = @TicketId
        ORDER BY a.AssignedAtUtc, a.AssignedUserId;

        SELECT h.HistoryId, h.ActorUserId,
               CONCAT(actor.Names, N' ', actor.LastNames) AS ActorDisplayName,
               h.RelatedUserId,
               CASE WHEN related.UserId IS NULL THEN NULL
                    ELSE CONCAT(related.Names, N' ', related.LastNames) END AS RelatedUserDisplayName,
               h.EventType, h.PreviousStatus, h.NewStatus, h.Comment, h.CreatedAtUtc
        FROM tickets.TicketHistory AS h
        INNER JOIN access.AppUser AS actor ON actor.UserId = h.ActorUserId
        LEFT JOIN access.AppUser AS related ON related.UserId = h.RelatedUserId
        WHERE h.TicketId = @TicketId
        ORDER BY h.CreatedAtUtc, h.HistoryId;
        """;
}
