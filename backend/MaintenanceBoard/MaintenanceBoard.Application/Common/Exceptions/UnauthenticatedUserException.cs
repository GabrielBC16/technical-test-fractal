namespace MaintenanceBoard.Application.Common.Exceptions;

public sealed class UnauthenticatedUserException(
    string message = "An active authenticated user is required.",
    Exception? innerException = null) : ApplicationFailureException("authentication.required", message, innerException);
