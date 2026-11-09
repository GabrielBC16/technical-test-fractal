using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace MaintenanceBoard.Api.Errors;

public static class ApiProblem
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static ProblemDetails Create(
        HttpContext context, int status, string detail, string code,
        IReadOnlyDictionary<string, string[]>? errors = null)
    {
        ProblemDetails problem = errors is null
            ? new ProblemDetails()
            : new ValidationProblemDetails(errors.ToDictionary(item => item.Key, item => item.Value));

        problem.Type = "about:blank";
        problem.Title = ReasonPhrases.GetReasonPhrase(status);
        problem.Status = status;
        problem.Detail = detail;
        problem.Instance = context.Request.Path;
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;
        return problem;
    }

    public static Task WriteAsync(
        HttpContext context, int status, string detail, string code,
        IReadOnlyDictionary<string, string[]>? errors = null)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        var problem = Create(context, status, detail, code, errors);
        return JsonSerializer.SerializeAsync(context.Response.Body, problem, problem.GetType(),
            JsonOptions, context.RequestAborted);
    }

    public static IActionResult ModelValidation(ActionContext context)
    {
        var errors = context.ModelState
            .Where(item => item.Value is { Errors.Count: > 0 })
            .ToDictionary(item => item.Key, item => item.Value!.Errors
                .Select(error => error.Exception is not null || string.IsNullOrWhiteSpace(error.ErrorMessage)
                    ? "The supplied value is invalid." : error.ErrorMessage)
                .ToArray());

        return new ObjectResult(Create(context.HttpContext, StatusCodes.Status400BadRequest,
            "One or more validation errors occurred.", "request.validation_failed", errors))
        {
            StatusCode = StatusCodes.Status400BadRequest,
            ContentTypes = { "application/problem+json" }
        };
    }
}
