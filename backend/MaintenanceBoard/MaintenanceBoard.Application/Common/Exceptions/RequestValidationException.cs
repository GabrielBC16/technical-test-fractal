namespace MaintenanceBoard.Application.Common.Exceptions;

public sealed class RequestValidationException(
    IReadOnlyDictionary<string, string[]> errors,
    string message = "One or more validation errors occurred.") : ApplicationFailureException("request.validation_failed", message)
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;

    public RequestValidationException(string field, string message)
        : this(new Dictionary<string, string[]> { [field] = [message] })
    {
    }
}
