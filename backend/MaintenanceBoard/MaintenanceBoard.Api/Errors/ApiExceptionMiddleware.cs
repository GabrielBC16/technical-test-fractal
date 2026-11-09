using MaintenanceBoard.Application.Common.Exceptions;
using MaintenanceBoard.Application.Tickets.Exceptions;

namespace MaintenanceBoard.Api.Errors;

public sealed class ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // A disconnected client does not represent an internal server error.
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = 499;
            }
        }
        catch (Exception exception)
        {
            if (context.Response.HasStarted)
            {
                throw;
            }

            var status = StatusFor(exception);
            if (status == StatusCodes.Status500InternalServerError)
            {
                logger.LogError(exception, "An unexpected failure occurred for {Method} {Path}.",
                    context.Request.Method, context.Request.Path);
            }

            context.Response.Clear();
            var known = exception as ApplicationFailureException;
            await ApiProblem.WriteAsync(context, status,
                status == StatusCodes.Status500InternalServerError
                    ? "The request could not be completed. Contact support with the trace ID."
                    : exception.Message,
                status == StatusCodes.Status500InternalServerError ? "server.unexpected_error" : known!.Code,
                (exception as RequestValidationException)?.Errors);
        }
    }

    private static int StatusFor(Exception exception) => exception switch
    {
        RequestValidationException => StatusCodes.Status400BadRequest,
        UnauthenticatedUserException => StatusCodes.Status401Unauthorized,
        ForbiddenOperationException => StatusCodes.Status403Forbidden,
        TicketNotFoundException => StatusCodes.Status404NotFound,
        InvalidTicketTransitionException or TicketConcurrencyException or TicketAssignmentLimitException or
            TicketAssignmentConflictException or ImmutableTicketHistoryException => StatusCodes.Status409Conflict,
        TicketCauseRequiredException or TicketRejectionReasonRequiredException or
            TicketAssignmentJustificationRequiredException or InvalidTicketAssigneeException or
            TicketCommentRequiredException => StatusCodes.Status422UnprocessableEntity,
        _ => StatusCodes.Status500InternalServerError
    };
}
