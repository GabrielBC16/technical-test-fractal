using MaintenanceBoard.Application.Common.Exceptions;

namespace MaintenanceBoard.Application.Tickets.Exceptions;

public sealed class TicketConcurrencyException(
    string message = "The ticket status changed. Reload the ticket before retrying.",
    Exception? innerException = null) : ApplicationFailureException("ticket.concurrent_modification", message, innerException);
