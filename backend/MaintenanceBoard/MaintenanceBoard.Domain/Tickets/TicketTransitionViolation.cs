namespace MaintenanceBoard.Domain.Tickets;

public enum TicketTransitionViolation
{
    InvalidTransition,
    AssigneeRequired,
    CauseRequired,
    RejectionReasonRequired
}
