using MaintenanceBoard.Application.Common.Exceptions;

namespace MaintenanceBoard.Application.Tickets.Exceptions;

public sealed class TicketAssignmentLimitException(
    string message = "A ticket cannot have more than three assignees.",
    Exception? innerException = null) : ApplicationFailureException("ticket.assignment_limit", message, innerException);
