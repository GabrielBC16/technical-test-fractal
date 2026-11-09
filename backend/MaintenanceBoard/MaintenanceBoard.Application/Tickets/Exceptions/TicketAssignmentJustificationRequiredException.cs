using MaintenanceBoard.Application.Common.Exceptions;

namespace MaintenanceBoard.Application.Tickets.Exceptions;

public sealed class TicketAssignmentJustificationRequiredException(
    string message = "A justification is required for additional assignees.",
    Exception? innerException = null) : ApplicationFailureException("ticket.assignment_justification_required", message, innerException);
