using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MaintenanceBoard.Domain.Access;
using MaintenanceBoard.Application.Access;
using MaintenanceBoard.Infrastructure.Development;
using MaintenanceBoard.Tests.Api;
using Microsoft.Extensions.DependencyInjection;

namespace MaintenanceBoard.Tests.Integration;

[Collection("SQL Server integration")]
[Trait("Category", "Integration")]
public sealed class ApiSqlIntegrationTests
{
    [SqlServerFact]
    public async Task HTTP_endpoints_execute_the_complete_workflow_with_real_authentication_and_persistence()
    {
        var context = await SqlIntegrationContext.CreateAsync();
        await using var factory = new SqlApiTestFactory();
        using var client = factory.Client();

        using var login = await client.PostAsJsonAsync("/api/auth/login",
            new { userName = context.Requester.UserName, password = context.Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var identity = await login.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(context.Requester.UserId, identity.GetProperty("userId").GetInt32());
        Assert.Equal("Requester", identity.GetProperty("role").GetString());

        SetUser(client, context.Requester.UserId);
        using var created = await client.PostAsJsonAsync("/api/tickets",
            new { title = "HTTP maintenance validation", description = "A validation-only terminal issue." });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("ticketId").GetInt32();
        Assert.Equal($"/api/tickets/{id}", created.Headers.Location?.OriginalString);

        using var denied = await client.PostAsJsonAsync($"/api/tickets/{id}/assignments",
            new { assignedUserId = context.Operator.UserId });
        await ApiAuthenticationAndValidationTests.AssertProblemAsync(denied, HttpStatusCode.Forbidden);

        SetUser(client, context.Operator.UserId);
        var users = await client.GetFromJsonAsync<JsonElement>("/api/users");
        Assert.Contains(users.EnumerateArray(), user => user.GetProperty("userId").GetInt32() == context.Operator.UserId);
        Assert.DoesNotContain(users.EnumerateArray(), user => user.GetProperty("role").GetString() == "Requester");
        using var assigned = await client.PostAsJsonAsync($"/api/tickets/{id}/assignments",
            new { assignedUserId = context.Operator.UserId });
        Assert.Equal(HttpStatusCode.NoContent, assigned.StatusCode);

        using var missingJustification = await client.PostAsJsonAsync($"/api/tickets/{id}/assignments",
            new { assignedUserId = context.Supervisor.UserId });
        var assignmentProblem = await ApiAuthenticationAndValidationTests.AssertProblemAsync(
            missingJustification, HttpStatusCode.UnprocessableEntity);
        Assert.Equal("ticket.assignment_justification_required", assignmentProblem.GetProperty("code").GetString());
        using var secondAssigned = await client.PostAsJsonAsync($"/api/tickets/{id}/assignments",
            new { assignedUserId = context.Supervisor.UserId, justification = "Specialist supervision required." });
        Assert.Equal(HttpStatusCode.NoContent, secondAssigned.StatusCode);

        using var skip = await client.PostAsJsonAsync($"/api/tickets/{id}/transitions",
            new { expectedStatus = "Pending", newStatus = "Closed", cause = "Known cause." });
        await ApiAuthenticationAndValidationTests.AssertProblemAsync(skip, HttpStatusCode.Conflict);
        using var started = await client.PostAsJsonAsync($"/api/tickets/{id}/transitions",
            new { expectedStatus = "Pending", newStatus = "InProgress" });
        Assert.Equal(HttpStatusCode.NoContent, started.StatusCode);

        using var stale = await client.PostAsJsonAsync($"/api/tickets/{id}/transitions",
            new { expectedStatus = "Pending", newStatus = "Closed", cause = "Known cause." });
        var conflict = await ApiAuthenticationAndValidationTests.AssertProblemAsync(stale, HttpStatusCode.Conflict);
        Assert.Equal("ticket.concurrent_modification", conflict.GetProperty("code").GetString());
        using var noCause = await client.PostAsJsonAsync($"/api/tickets/{id}/transitions",
            new { expectedStatus = "InProgress", newStatus = "Closed" });
        await ApiAuthenticationAndValidationTests.AssertProblemAsync(noCause, HttpStatusCode.UnprocessableEntity);

        SetUser(client, context.Requester.UserId);
        using var comment = await client.PostAsJsonAsync($"/api/tickets/{id}/comments",
            new { comment = "The terminal is ready for the repair." });
        Assert.Equal(HttpStatusCode.NoContent, comment.StatusCode);
        SetUser(client, context.Supervisor.UserId);
        using var closed = await client.PostAsJsonAsync($"/api/tickets/{id}/transitions",
            new { expectedStatus = "InProgress", newStatus = "Closed", cause = "Power supply failure." });
        Assert.Equal(HttpStatusCode.NoContent, closed.StatusCode);

        SetUser(client, context.Requester.UserId);
        var details = await client.GetFromJsonAsync<JsonElement>($"/api/tickets/{id}");
        Assert.Equal("Closed", details.GetProperty("ticket").GetProperty("status").GetString());
        Assert.Equal(context.Requester.UserId, details.GetProperty("ticket").GetProperty("reportedByUserId").GetInt32());
        Assert.Equal("Power supply failure.", details.GetProperty("cause").GetString());
        Assert.Equal(2, details.GetProperty("ticket").GetProperty("assignees").GetArrayLength());
        Assert.Equal(6, details.GetProperty("history").GetArrayLength());
        Assert.Contains(details.GetProperty("history").EnumerateArray(), item =>
            item.GetProperty("eventType").GetString() == "Comment" &&
            item.GetProperty("actorUserId").GetInt32() == context.Requester.UserId);

        using var another = await client.PostAsJsonAsync("/api/tickets",
            new { title = "HTTP rejection validation", description = "A duplicate incident." });
        Assert.Equal(HttpStatusCode.Created, another.StatusCode);
        var rejectedId = (await another.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("ticketId").GetInt32();
        SetUser(client, context.Operator.UserId);
        using var noReason = await client.PostAsJsonAsync($"/api/tickets/{rejectedId}/transitions",
            new { expectedStatus = "Pending", newStatus = "Rejected" });
        await ApiAuthenticationAndValidationTests.AssertProblemAsync(noReason, HttpStatusCode.UnprocessableEntity);
        using var rejected = await client.PostAsJsonAsync($"/api/tickets/{rejectedId}/transitions",
            new { expectedStatus = "Pending", newStatus = "Rejected", comment = "Duplicate of another incident." });
        Assert.Equal(HttpStatusCode.NoContent, rejected.StatusCode);

        var board = await client.GetFromJsonAsync<JsonElement>("/api/tickets/board");
        Assert.False(board.TryGetProperty("rejected", out _));
        foreach (var state in new[] { "pending", "inProgress", "closed" })
        {
            Assert.DoesNotContain(board.GetProperty(state).EnumerateArray(),
                ticket => ticket.GetProperty("ticketId").GetInt32() == rejectedId);
        }

        var listing = await client.GetFromJsonAsync<JsonElement>("/api/tickets?status=Rejected");
        Assert.Contains(listing.EnumerateArray(), ticket => ticket.GetProperty("ticketId").GetInt32() == rejectedId);
        client.DefaultRequestHeaders.Remove("X-User-Id");
        using var missingIdentity = await client.GetAsync("/api/tickets/board");
        await ApiAuthenticationAndValidationTests.AssertProblemAsync(missingIdentity, HttpStatusCode.Unauthorized);
    }

    [SqlServerFact]
    public async Task Default_initializer_creates_the_requested_users_once_with_usable_credentials()
    {
        string[] userNames = ["gbarrantes", "fcabello", "mhuarcaya"];
        string[] names = ["Gabriel", "Fernando", "Mario"];
        string[] lastNames = ["Barrantes", "Cabello", "Huarcaya"];
        string[] roles = ["Requester", "Operator", "Supervisor"];
        var originalHashes = new Dictionary<string, string>();
        var originalIds = new Dictionary<string, int>();

        await using (var factory = new SqlApiTestFactory(initializeDevelopmentUsers: true))
        {
            using var client = factory.Client();
            using var scope = factory.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            for (var index = 0; index < userNames.Length; index++)
            {
                var userName = userNames[index];
                using var login = await client.PostAsJsonAsync("/api/auth/login", new { userName, password = userName });
                Assert.Equal(HttpStatusCode.OK, login.StatusCode);
                var identity = await login.Content.ReadFromJsonAsync<JsonElement>();
                Assert.Equal(names[index], identity.GetProperty("names").GetString());
                Assert.Equal(lastNames[index], identity.GetProperty("lastNames").GetString());
                Assert.Equal(roles[index], identity.GetProperty("role").GetString());
                var credentials = await users.GetCredentialsByUserNameAsync(userName);
                Assert.NotNull(credentials);
                Assert.NotEqual(userName, credentials.PasswordHash);
                originalHashes.Add(userName, credentials.PasswordHash);
                originalIds.Add(userName, credentials.User.UserId);
            }
        }

        await using var restarted = new SqlApiTestFactory(initializeDevelopmentUsers: true);
        using var restartedClient = restarted.Client();
        using var restartedScope = restarted.Services.CreateScope();
        var repository = restartedScope.ServiceProvider.GetRequiredService<IUserRepository>();
        foreach (var userName in userNames)
        {
            var credentials = await repository.GetCredentialsByUserNameAsync(userName);
            Assert.NotNull(credentials);
            Assert.Equal(originalIds[userName], credentials.User.UserId);
            Assert.Equal(originalHashes[userName], credentials.PasswordHash);
            var tickets = await restartedScope.ServiceProvider.GetRequiredService<MaintenanceBoard.Application.Tickets.ITicketRepository>()
                .GetListAsync(new MaintenanceBoard.Application.Tickets.TicketQueryFilter(null, null, null));
            Assert.DoesNotContain(tickets, ticket => ticket.ReportedByUserId == credentials.User.UserId);
        }
    }

    [SqlServerFact]
    public async Task Startup_seeding_reads_configuration_and_creates_usable_BCrypt_credentials()
    {
        var prefix = "http_seed_" + Guid.NewGuid().ToString("N");
        var password = Guid.NewGuid().ToString("N") + "Aa1!";
        var options = new DevelopmentSeedOptions([
            new(prefix + "_requester", "Test", "Requester", UserRole.Requester, password),
            new(prefix + "_operator", "Test", "Operator", UserRole.Operator, password),
            new(prefix + "_supervisor", "Test", "Supervisor", UserRole.Supervisor, password)
        ], IncludeSampleTickets: true);
        await using var factory = new SqlApiTestFactory(options);
        using var client = factory.Client();

        using var login = await client.PostAsJsonAsync("/api/auth/login",
            new { userName = options.Users[0].UserName, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var identity = await login.Content.ReadFromJsonAsync<JsonElement>();
        SetUser(client, identity.GetProperty("userId").GetInt32());
        var tickets = await client.GetFromJsonAsync<JsonElement>("/api/tickets");
        var ownTickets = tickets.EnumerateArray().Where(ticket =>
            ticket.GetProperty("reportedByUserId").GetInt32() == identity.GetProperty("userId").GetInt32()).ToArray();
        Assert.Equal(4, ownTickets.Length);
        Assert.Equal(4, ownTickets.Select(ticket => ticket.GetProperty("status").GetString()).Distinct().Count());
    }

    private static void SetUser(HttpClient client, int userId)
    {
        client.DefaultRequestHeaders.Remove("X-User-Id");
        client.DefaultRequestHeaders.Add("X-User-Id", userId.ToString(CultureInfo.InvariantCulture));
    }
}
