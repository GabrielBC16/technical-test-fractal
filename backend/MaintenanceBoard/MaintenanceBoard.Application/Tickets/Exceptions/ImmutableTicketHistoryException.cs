using MaintenanceBoard.Application.Common.Exceptions;

namespace MaintenanceBoard.Application.Tickets.Exceptions;

public sealed class ImmutableTicketHistoryException(
    string message = "Ticket history is append-only and cannot be updated or deleted.",
    Exception? innerException = null) : ApplicationFailureException("ticket.history_immutable", message, innerException);
