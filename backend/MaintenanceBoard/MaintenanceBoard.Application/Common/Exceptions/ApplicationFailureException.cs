namespace MaintenanceBoard.Application.Common.Exceptions;

public abstract class ApplicationFailureException(
    string code,
    string message,
    Exception? innerException = null) : Exception(message, innerException)
{
    public string Code { get; } = code;
}
