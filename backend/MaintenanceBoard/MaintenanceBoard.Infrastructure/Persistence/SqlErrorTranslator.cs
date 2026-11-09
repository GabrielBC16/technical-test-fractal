using MaintenanceBoard.Application.Common.Exceptions;
using MaintenanceBoard.Application.Tickets.Exceptions;
using Microsoft.Data.SqlClient;

namespace MaintenanceBoard.Infrastructure.Persistence;

public static class SqlErrorTranslator
{
    public static ApplicationFailureException? Translate(SqlException exception)
    {
        foreach (SqlError error in exception.Errors)
        {
            var translated = TranslateNumber(error.Number, error.Message, exception);
            if (translated is not null)
            {
                return translated;
            }
        }

        // Connection failures and unexpected SQL errors remain operational failures.
        return null;
    }

    internal static ApplicationFailureException? TranslateNumber(int number, string message, Exception? innerException = null) =>
        number switch
        {
            50001 => new TicketNotFoundException(message, innerException),
            50002 => new InvalidTicketTransitionException(message, innerException),
            50003 => new TicketConcurrencyException(message, innerException),
            50004 => new TicketCauseRequiredException(message, innerException),
            50005 => new TicketRejectionReasonRequiredException(message, innerException),
            50006 => new TicketAssignmentLimitException(message, innerException),
            50007 => new TicketAssignmentJustificationRequiredException(message, innerException),
            50008 => new InvalidTicketAssigneeException(message, innerException),
            50009 => new TicketAssignmentConflictException(message, innerException),
            50010 => new UnauthenticatedUserException(message, innerException),
            50011 => new ForbiddenOperationException(message, innerException),
            50012 => new RequestValidationException("request", message),
            50013 => new TicketCommentRequiredException(message, innerException),
            50014 => new ImmutableTicketHistoryException(message, innerException),
            _ => null
        };
}
