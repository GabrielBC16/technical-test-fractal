using MaintenanceBoard.Application.Common.Exceptions;

namespace MaintenanceBoard.Application.Tickets.Exceptions;

public sealed class TicketCauseRequiredException(
    string message = "A non-empty cause is required to close the ticket.",
    Exception? innerException = null) : ApplicationFailureException("ticket.cause_required", message, innerException);
