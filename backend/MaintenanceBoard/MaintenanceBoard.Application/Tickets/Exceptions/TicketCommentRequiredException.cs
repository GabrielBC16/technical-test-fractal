using MaintenanceBoard.Application.Common.Exceptions;

namespace MaintenanceBoard.Application.Tickets.Exceptions;

public sealed class TicketCommentRequiredException(
    string message = "A non-empty comment is required.",
    Exception? innerException = null) : ApplicationFailureException("ticket.comment_required", message, innerException);
