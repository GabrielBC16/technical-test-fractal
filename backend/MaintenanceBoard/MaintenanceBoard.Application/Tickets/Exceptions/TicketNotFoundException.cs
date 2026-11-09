using MaintenanceBoard.Application.Common.Exceptions;

namespace MaintenanceBoard.Application.Tickets.Exceptions;

public sealed class TicketNotFoundException(
    string message = "The ticket was not found.",
    Exception? innerException = null) : ApplicationFailureException("ticket.not_found", message, innerException);
