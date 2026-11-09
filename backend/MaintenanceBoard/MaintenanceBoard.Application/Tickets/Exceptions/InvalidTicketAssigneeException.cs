using MaintenanceBoard.Application.Common.Exceptions;

namespace MaintenanceBoard.Application.Tickets.Exceptions;

public sealed class InvalidTicketAssigneeException(
    string message = "The assignee must be an active operator or supervisor.",
    Exception? innerException = null) : ApplicationFailureException("ticket.invalid_assignee", message, innerException);
