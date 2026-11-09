using MaintenanceBoard.Application.Common.Exceptions;

namespace MaintenanceBoard.Application.Tickets.Exceptions;

public sealed class TicketAssignmentConflictException(
    string message = "The ticket is not open for assignment or the user is already assigned.",
    Exception? innerException = null) : ApplicationFailureException("ticket.assignment_conflict", message, innerException);
