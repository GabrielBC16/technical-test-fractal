using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MaintenanceBoard.Application.Common.Exceptions;
using MaintenanceBoard.Application.Tickets.Exceptions;
using MaintenanceBoard.Domain.Tickets;
using MaintenanceBoard.Tests.Fakes;

namespace MaintenanceBoard.Tests.Api;

public sealed class ApiErrorsCorsAndSwaggerTests
{
    [Theory]
    [InlineData("not-found", 404)]
    [InlineData("transition", 409)]
    [InlineData("concurrency", 409)]
    [InlineData("limit", 409)]
    [InlineData("assignment", 409)]
    [InlineData("history", 409)]
    [InlineData("cause", 422)]
    [InlineData("reason", 422)]
    [InlineData("justification", 422)]
    [InlineData("assignee", 422)]
    [InlineData("comment", 422)]
    [InlineData("validation", 400)]
    [InlineData("authentication", 401)]
    [InlineData("authorization", 403)]
    public async Task Application_failures_have_their_expected_HTTP_status(string failure, int status)
    {
        await using var factory = new ApiTestFactory();
        factory.Context.Tickets.WriteException = Failure(failure);
        using var client = factory.Client("20");

        using var response = await client.PostAsJsonAsync("/api/tickets", new { title = "Title", description = "Description" });

        var problem = await ApiAuthenticationAndValidationTests.AssertProblemAsync(response, (HttpStatusCode)status);
        Assert.NotEqual("server.unexpected_error", problem.GetProperty("code").GetString());
        if (status == 400)
        {
            Assert.True(problem.TryGetProperty("errors", out _));
        }
    }

    [Fact]
    public async Task Unexpected_failures_are_logged_but_not_disclosed_to_the_client()
    {
        await using var factory = new ApiTestFactory();
        factory.Context.Tickets.WriteException = new InvalidOperationException("INTERNAL_FAILURE_MARKER");
        using var client = factory.Client("20");

        using var response = await client.PostAsJsonAsync("/api/tickets", new { title = "Title", description = "Description" });

        var problem = await ApiAuthenticationAndValidationTests.AssertProblemAsync(response, HttpStatusCode.InternalServerError);
        Assert.Equal("server.unexpected_error", problem.GetProperty("code").GetString());
        Assert.DoesNotContain("INTERNAL_FAILURE_MARKER", problem.GetRawText());
    }

    [Fact]
    public async Task Board_and_detail_responses_use_string_statuses_and_ordered_history()
    {
        await using var factory = new ApiTestFactory();
        factory.Context.Tickets.BoardItems = [
            ApplicationTestData.ListItem(TicketStatus.Pending),
            ApplicationTestData.ListItem(TicketStatus.Rejected, 101)];
        factory.Context.Tickets.Details = new(
            ApplicationTestData.ListItem(TicketStatus.Pending), null,
            [ApplicationTestData.History(2), ApplicationTestData.History(1)]);
        using var client = factory.Client("20");

        var board = await client.GetFromJsonAsync<JsonElement>("/api/tickets/board");
        Assert.Equal("Pending", board.GetProperty("pending")[0].GetProperty("status").GetString());
        Assert.False(board.TryGetProperty("rejected", out _));
        var details = await client.GetFromJsonAsync<JsonElement>("/api/tickets/100");
        Assert.Equal(1, details.GetProperty("history")[0].GetProperty("historyId").GetInt64());
        Assert.Equal("Comment", details.GetProperty("history")[0].GetProperty("eventType").GetString());
    }

    [Theory]
    [InlineData("/api/tickets?from=not-a-date")]
    [InlineData("/api/tickets?status=unknown")]
    [InlineData("/api/tickets?from=2026-11-09T00:00:00Z&to=2026-11-08T00:00:00Z")]
    [InlineData("/api/tickets/0")]
    public async Task Invalid_queries_or_ids_are_validation_errors(string route)
    {
        await using var factory = new ApiTestFactory();
        using var client = factory.Client("20");

        using var response = await client.GetAsync(route);

        var problem = await ApiAuthenticationAndValidationTests.AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.True(problem.TryGetProperty("errors", out _));
    }

    [Theory]
    [InlineData("https://frontend.example", true)]
    [InlineData("https://untrusted.example", false)]
    public async Task CORS_preflight_is_anonymous_and_limited_to_configured_origins(string origin, bool allowed)
    {
        await using var factory = new ApiTestFactory();
        using var client = factory.Client();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/tickets");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type,x-user-id");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(allowed, response.Headers.Contains("Access-Control-Allow-Origin"));
        if (allowed)
        {
            Assert.Equal(origin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        }
    }

    [Fact]
    public async Task Swagger_is_available_in_Development_and_documents_the_header_and_all_routes()
    {
        await using var factory = new ApiTestFactory();
        using var client = factory.Client();

        using var response = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("MaintenanceBoard API", document.GetProperty("info").GetProperty("title").GetString());
        var scheme = document.GetProperty("components").GetProperty("securitySchemes").GetProperty("SimulatedUser");
        Assert.Equal("X-User-Id", scheme.GetProperty("name").GetString());
        Assert.Equal("header", scheme.GetProperty("in").GetString());
        var paths = document.GetProperty("paths");
        Assert.Equal(8, paths.EnumerateObject().Count());
        Assert.True(paths.GetProperty("/api/tickets").TryGetProperty("get", out _));
        Assert.True(paths.GetProperty("/api/tickets").TryGetProperty("post", out _));
        Assert.Equal(0, paths.GetProperty("/api/auth/login").GetProperty("post").GetProperty("security").GetArrayLength());
        using var ui = await client.GetAsync("/swagger/index.html");
        Assert.Equal(HttpStatusCode.OK, ui.StatusCode);
    }

    [Fact]
    public async Task Application_error_responses_keep_the_allowed_CORS_origin()
    {
        await using var factory = new ApiTestFactory();
        using var client = factory.Client("10");
        client.DefaultRequestHeaders.Add("Origin", "https://frontend.example");

        using var response = await client.PostAsJsonAsync("/api/tickets/100/transitions",
            new { expectedStatus = "Pending", newStatus = "Closed", cause = "Cause." });

        await ApiAuthenticationAndValidationTests.AssertProblemAsync(response, HttpStatusCode.Conflict);
        Assert.Equal("https://frontend.example", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    [Fact]
    public async Task Production_refuses_development_seeding_at_startup()
    {
        await using var factory = new ApiTestFactory("Production", enableSeed: true);

        var exception = Assert.Throws<InvalidOperationException>(() => factory.Client());

        Assert.Contains("cannot be enabled outside Development", exception.Message);
        Assert.Equal(0, factory.Context.Tickets.WriteCalls);
    }

    [Fact]
    public async Task Production_never_initializes_the_local_demo_accounts()
    {
        await using var factory = new ApiTestFactory("Production", initializeDevelopmentUsers: true);
        using var client = factory.Client("20");

        using var response = await client.GetAsync("/api/tickets/board");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, factory.Context.Tickets.WriteCalls);
    }

    [Fact]
    public async Task Swagger_is_not_exposed_in_Production()
    {
        await using var factory = new ApiTestFactory("Production");
        using var client = factory.Client("20");

        using var response = await client.GetAsync("/swagger/v1/swagger.json");

        await ApiAuthenticationAndValidationTests.AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    private static Exception Failure(string name) => name switch
    {
        "not-found" => new TicketNotFoundException(),
        "transition" => new InvalidTicketTransitionException(),
        "concurrency" => new TicketConcurrencyException(),
        "limit" => new TicketAssignmentLimitException(),
        "assignment" => new TicketAssignmentConflictException(),
        "history" => new ImmutableTicketHistoryException(),
        "cause" => new TicketCauseRequiredException(),
        "reason" => new TicketRejectionReasonRequiredException(),
        "justification" => new TicketAssignmentJustificationRequiredException(),
        "assignee" => new InvalidTicketAssigneeException(),
        "comment" => new TicketCommentRequiredException(),
        "validation" => new RequestValidationException("title", "Title is invalid."),
        "authentication" => new UnauthenticatedUserException(),
        "authorization" => new ForbiddenOperationException(),
        _ => throw new ArgumentOutOfRangeException(nameof(name))
    };
}
