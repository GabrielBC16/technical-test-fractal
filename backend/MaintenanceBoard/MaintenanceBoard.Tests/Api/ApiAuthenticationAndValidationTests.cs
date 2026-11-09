using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MaintenanceBoard.Tests.Fakes;

namespace MaintenanceBoard.Tests.Api;

public sealed class ApiAuthenticationAndValidationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("not-an-id")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("10,20")]
    [InlineData("80")]
    [InlineData("999")]
    public async Task Missing_invalid_unknown_and_inactive_users_receive_401_ProblemDetails(string? header)
    {
        await using var factory = new ApiTestFactory();
        using var client = factory.Client(header);

        using var response = await client.GetAsync("/api/tickets/board");

        var problem = await AssertProblemAsync(response, HttpStatusCode.Unauthorized);
        Assert.Equal("authentication.required", problem.GetProperty("code").GetString());
        Assert.Contains(response.Headers.WwwAuthenticate, item => item.Scheme == "SimulatedUser");
        Assert.Equal(0, factory.Context.Tickets.ReadCalls);
    }

    [Fact]
    public async Task Multiple_identity_headers_are_rejected()
    {
        await using var factory = new ApiTestFactory();
        using var client = factory.Client();
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-User-Id", ["10", "20"]);

        using var response = await client.GetAsync("/api/tickets/board");

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("/api/tickets/100/assignments", "{\"assignedUserId\":30}")]
    [InlineData("/api/tickets/100/transitions", "{\"expectedStatus\":\"Pending\",\"newStatus\":\"Rejected\",\"comment\":\"Duplicate.\"}")]
    public async Task Requesters_cannot_assign_or_change_status_even_on_their_own_ticket(string route, string json)
    {
        await using var factory = new ApiTestFactory();
        using var client = factory.Client("20");

        using var response = await client.PostAsync(route, new StringContent(json, Encoding.UTF8, "application/json"));

        await AssertProblemAsync(response, HttpStatusCode.Forbidden);
        Assert.Equal(0, factory.Context.Tickets.WriteCalls);
        Assert.Equal(0, factory.Context.Tickets.ReadCalls);
    }

    [Fact]
    public async Task Requesters_cannot_list_assignees()
    {
        await using var factory = new ApiTestFactory();
        using var client = factory.Client("20");

        using var response = await client.GetAsync("/api/users");

        await AssertProblemAsync(response, HttpStatusCode.Forbidden);
        Assert.Equal(0, factory.Context.Users.CandidateCalls);
    }

    [Fact]
    public async Task Creation_uses_the_authenticated_reporter_and_returns_the_resource_location()
    {
        await using var factory = new ApiTestFactory();
        using var client = factory.Client("20");

        using var response = await client.PostAsJsonAsync("/api/tickets", new { title = "Title", description = "Description" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/api/tickets/123", response.Headers.Location?.OriginalString);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(123, payload.GetProperty("ticketId").GetInt32());
        Assert.Equal(20, factory.Context.Tickets.Created!.ReportedByUserId);
    }

    [Theory]
    [InlineData("{\"description\":\"Description\"}")]
    [InlineData("{\"title\":\"Title\"}")]
    [InlineData("{\"title\":\" \",\"description\":\"Description\"}")]
    [InlineData("{\"title\":123,\"description\":\"Description\"}")]
    [InlineData("null")]
    [InlineData("{")]
    public async Task Invalid_models_receive_400_with_field_details_without_writing(string json)
    {
        await using var factory = new ApiTestFactory();
        using var client = factory.Client("20");

        using var response = await client.PostAsync("/api/tickets", new StringContent(json, Encoding.UTF8, "application/json"));

        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.NotEmpty(problem.GetProperty("errors").EnumerateObject());
        Assert.Equal(0, factory.Context.Tickets.WriteCalls);
    }

    [Theory]
    [InlineData("reportedByUserId")]
    [InlineData("actorUserId")]
    [InlineData("assignedByUserId")]
    public async Task Actor_identity_fields_are_not_accepted_in_ticket_creation(string field)
    {
        await using var factory = new ApiTestFactory();
        using var client = factory.Client("20");
        var body = new Dictionary<string, object> { ["title"] = "Title", ["description"] = "Description", [field] = 40 };

        using var response = await client.PostAsJsonAsync("/api/tickets", body);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Equal(0, factory.Context.Tickets.WriteCalls);
    }

    [Fact]
    public async Task Assignment_uses_the_principal_as_actor_and_the_body_only_as_target()
    {
        await using var factory = new ApiTestFactory();
        using var client = factory.Client("10");

        using var response = await client.PostAsJsonAsync("/api/tickets/100/assignments", new { assignedUserId = 30 });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(10, factory.Context.Tickets.Assigned!.ActorUserId);
        Assert.Equal(30, factory.Context.Tickets.Assigned.AssignedUserId);
    }

    [Fact]
    public async Task Supervisor_status_changes_use_the_principal_identity()
    {
        await using var factory = new ApiTestFactory();
        factory.Context.Tickets.Assignments.Add(ApplicationTestData.Assignment(30));
        using var client = factory.Client("40");

        using var response = await client.PostAsJsonAsync("/api/tickets/100/transitions",
            new { expectedStatus = "Pending", newStatus = "InProgress" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(40, factory.Context.Tickets.Transitioned!.ActorUserId);
    }

    [Fact]
    public async Task Requester_comment_ownership_is_enforced_by_the_application()
    {
        await using var factory = new ApiTestFactory();
        factory.Context.Tickets.Ticket = ApplicationTestData.Ticket(reportedByUserId: 70);
        using var client = factory.Client("20");

        using var response = await client.PostAsJsonAsync("/api/tickets/100/comments", new { comment = "An update." });

        await AssertProblemAsync(response, HttpStatusCode.Forbidden);
        Assert.Equal(0, factory.Context.Tickets.WriteCalls);
    }

    [Fact]
    public async Task Login_is_anonymous_and_does_not_return_a_password_hash_or_token()
    {
        await using var factory = new ApiTestFactory();
        using var client = factory.Client();

        using var response = await client.PostAsJsonAsync("/api/auth/login",
            new { userName = "user20", password = factory.Passwords.ExpectedPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(20, payload.GetProperty("userId").GetInt32());
        Assert.Equal("Requester", payload.GetProperty("role").GetString());
        Assert.False(payload.TryGetProperty("passwordHash", out _));
        Assert.False(payload.TryGetProperty("token", out _));
    }

    [Fact]
    public async Task Invalid_login_credentials_receive_401()
    {
        await using var factory = new ApiTestFactory();
        using var client = factory.Client();

        using var response = await client.PostAsJsonAsync("/api/auth/login",
            new { userName = "user20", password = "incorrect-validation-password" });

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized);
    }

    internal static async Task<JsonElement> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)status, payload.GetProperty("status").GetInt32());
        Assert.Equal("about:blank", payload.GetProperty("type").GetString());
        Assert.False(string.IsNullOrEmpty(payload.GetProperty("traceId").GetString()));
        Assert.True(payload.TryGetProperty("title", out _));
        Assert.True(payload.TryGetProperty("detail", out _));
        Assert.True(payload.TryGetProperty("instance", out _));
        return payload;
    }
}
