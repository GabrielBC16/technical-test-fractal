using MaintenanceBoard.Application.Common.Exceptions;

namespace MaintenanceBoard.Application.Tickets.Exceptions;

public sealed class TicketRejectionReasonRequiredException(
    string message = "A non-empty rejection reason is required.",
    Exception? innerException = null) : ApplicationFailureException("ticket.rejection_reason_required", message, innerException);
