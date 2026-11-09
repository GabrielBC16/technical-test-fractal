using MaintenanceBoard.Application.Common.Exceptions;

namespace MaintenanceBoard.Application.Tickets.Exceptions;

public sealed class InvalidTicketTransitionException(
    string message = "The requested ticket status transition is not allowed.",
    Exception? innerException = null) : ApplicationFailureException("ticket.invalid_transition", message, innerException);
