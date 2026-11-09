namespace MaintenanceBoard.Application.Common.Exceptions;

public sealed class ForbiddenOperationException(
    string message = "The user is not allowed to perform this operation.",
    Exception? innerException = null) : ApplicationFailureException("access.forbidden", message, innerException);
