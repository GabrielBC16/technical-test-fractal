namespace MaintenanceBoard.Domain.Tickets;

// Application translates domain violations into its own exceptions; Domain knows no HTTP or SQL codes.
public sealed class TicketTransitionException(
    TicketTransitionViolation violation,
    string message) : Exception(message)
{
    public TicketTransitionViolation Violation { get; } = violation;
}
