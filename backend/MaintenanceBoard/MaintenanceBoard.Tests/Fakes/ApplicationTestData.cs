using MaintenanceBoard.Application.Tickets;
using MaintenanceBoard.Domain.Access;
using MaintenanceBoard.Domain.Tickets;

namespace MaintenanceBoard.Tests.Fakes;

internal static class ApplicationTestData
{
    public static readonly DateTime Timestamp = new(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);

    public static AppUser User(int userId = 10, UserRole role = UserRole.Operator, bool isActive = true) =>
        new(userId, $"user{userId}", "Test", $"User{userId}", role, isActive, Timestamp);

    public static Ticket Ticket(TicketStatus status = TicketStatus.Pending, int reportedByUserId = 20) =>
        new(100, "Power failure", "The equipment is offline.", status, null, reportedByUserId, Timestamp, Timestamp);

    public static TicketAssignment Assignment(int userId) =>
        new(100, userId, 10, null, Timestamp);

    public static TicketListItem ListItem(TicketStatus status, int ticketId = 100) =>
        new(ticketId, "Power failure", "The equipment is offline.", status, 20, "Test Requester", Timestamp, Timestamp, []);

    public static TicketHistoryItem History(long historyId, DateTime? timestamp = null) =>
        new(historyId, 10, "Test Operator", null, null, TicketEventType.Comment, null, null, "An update.", timestamp ?? Timestamp);
}
