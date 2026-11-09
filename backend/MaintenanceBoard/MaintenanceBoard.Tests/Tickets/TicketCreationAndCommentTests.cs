using MaintenanceBoard.Application.Common.Exceptions;
using MaintenanceBoard.Application.Tickets;
using MaintenanceBoard.Application.Tickets.Exceptions;
using MaintenanceBoard.Domain.Access;
using MaintenanceBoard.Domain.Tickets;
using MaintenanceBoard.Tests.Fakes;

namespace MaintenanceBoard.Tests.Tickets;

public sealed class TicketCreationAndCommentTests
{
    [Theory]
    [InlineData(UserRole.Requester)]
    [InlineData(UserRole.Operator)]
    [InlineData(UserRole.Supervisor)]
    public async Task Every_active_role_can_create_a_ticket_using_the_authenticated_actor(UserRole role)
    {
        var context = new TicketServiceContext(role);

        var ticketId = await context.Service.CreateAsync(new CreateTicketRequest("  Power failure  ", "  Equipment offline.  "));

        Assert.Equal(123, ticketId);
        var call = Assert.IsType<FakeTicketRepository.CreateCall>(context.Tickets.Created);
        Assert.Equal(10, call.ReportedByUserId);
        Assert.Equal("Power failure", call.Title);
        Assert.Equal("Equipment offline.", call.Description);
        Assert.Equal(1, context.Tickets.WriteCalls);
        Assert.Null(context.Tickets.Assigned);
    }

    [Theory]
    [InlineData(null, "Description", "title")]
    [InlineData(" ", "Description", "title")]
    [InlineData("Title", null, "description")]
    [InlineData("Title", "\t", "description")]
    public async Task Creation_rejects_missing_text_without_writing(string? title, string? description, string field)
    {
        var context = new TicketServiceContext();

        var exception = await Assert.ThrowsAsync<RequestValidationException>(() =>
            context.Service.CreateAsync(new CreateTicketRequest(title, description)));

        Assert.Contains(field, exception.Errors.Keys);
        Assert.Equal(0, context.Tickets.WriteCalls);
    }

    [Theory]
    [InlineData(201, 10, "title")]
    [InlineData(10, 4001, "description")]
    public async Task Creation_rejects_oversized_text_without_writing(int titleLength, int descriptionLength, string field)
    {
        var context = new TicketServiceContext();

        var exception = await Assert.ThrowsAsync<RequestValidationException>(() => context.Service.CreateAsync(
            new CreateTicketRequest(new string('x', titleLength), new string('x', descriptionLength))));

        Assert.Contains(field, exception.Errors.Keys);
        Assert.Equal(0, context.Tickets.WriteCalls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(999)]
    [InlineData(80)]
    public async Task Creation_rejects_missing_unknown_or_inactive_actor(int? userId)
    {
        var context = new TicketServiceContext();
        context.CurrentUser.UserId = userId;

        await Assert.ThrowsAsync<UnauthenticatedUserException>(() =>
            context.Service.CreateAsync(new CreateTicketRequest("Title", "Description")));

        Assert.Equal(0, context.Tickets.WriteCalls);
    }

    [Theory]
    [InlineData(UserRole.Requester)]
    [InlineData(UserRole.Operator)]
    [InlineData(UserRole.Supervisor)]
    public async Task All_roles_can_comment_on_their_own_ticket(UserRole role)
    {
        var context = new TicketServiceContext(role);
        context.Tickets.Ticket = ApplicationTestData.Ticket(reportedByUserId: 10);

        await context.Service.AddCommentAsync(100, new AddTicketCommentRequest("  Equipment is still offline.  "));

        var call = Assert.IsType<FakeTicketRepository.CommentCall>(context.Tickets.Commented);
        Assert.Equal(10, call.ActorUserId);
        Assert.Equal("Equipment is still offline.", call.Comment);
        Assert.Null(context.Tickets.Transitioned);
    }

    [Fact]
    public async Task A_requester_cannot_comment_on_another_users_ticket()
    {
        var context = new TicketServiceContext(UserRole.Requester);

        await Assert.ThrowsAsync<ForbiddenOperationException>(() =>
            context.Service.AddCommentAsync(100, new AddTicketCommentRequest("An update.")));

        Assert.Equal(0, context.Tickets.WriteCalls);
    }

    [Theory]
    [InlineData(UserRole.Operator)]
    [InlineData(UserRole.Supervisor)]
    public async Task Operators_and_supervisors_can_comment_on_other_users_tickets(UserRole role)
    {
        var context = new TicketServiceContext(role);

        await context.Service.AddCommentAsync(100, new AddTicketCommentRequest("An update."));

        Assert.Equal(1, context.Tickets.WriteCalls);
    }

    [Theory]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Rejected)]
    public async Task Comments_remain_allowed_on_own_terminal_tickets(TicketStatus status)
    {
        var context = new TicketServiceContext(UserRole.Requester);
        context.Tickets.Ticket = ApplicationTestData.Ticket(status, reportedByUserId: 10);

        await context.Service.AddCommentAsync(100, new AddTicketCommentRequest("Acknowledged."));

        Assert.NotNull(context.Tickets.Commented);
        Assert.Null(context.Tickets.Transitioned);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n ")]
    [InlineData("\u202F")]
    public async Task Empty_comments_fail_before_writing(string? comment)
    {
        var context = new TicketServiceContext();

        await Assert.ThrowsAsync<TicketCommentRequiredException>(() =>
            context.Service.AddCommentAsync(100, new AddTicketCommentRequest(comment)));

        Assert.Equal(0, context.Tickets.WriteCalls);
    }

    [Fact]
    public async Task Oversized_comments_fail_before_writing()
    {
        var context = new TicketServiceContext();

        await Assert.ThrowsAsync<RequestValidationException>(() =>
            context.Service.AddCommentAsync(100, new AddTicketCommentRequest(new string('x', 4001))));

        Assert.Equal(0, context.Tickets.WriteCalls);
    }

    [Fact]
    public async Task Commenting_on_a_missing_ticket_returns_a_not_found_failure()
    {
        var context = new TicketServiceContext();

        await Assert.ThrowsAsync<TicketNotFoundException>(() =>
            context.Service.AddCommentAsync(999, new AddTicketCommentRequest("An update.")));

        Assert.Equal(0, context.Tickets.WriteCalls);
    }

    [Fact]
    public async Task Pre_cancelled_requests_do_not_read_or_write_repositories()
    {
        var context = new TicketServiceContext();
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            context.Service.CreateAsync(new CreateTicketRequest("Title", "Description"), source.Token));

        Assert.Equal(0, context.Users.LookupCalls);
        Assert.Equal(0, context.Tickets.WriteCalls);
    }
}
