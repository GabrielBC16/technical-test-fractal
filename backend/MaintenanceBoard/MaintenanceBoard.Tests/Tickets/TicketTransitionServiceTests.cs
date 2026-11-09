using MaintenanceBoard.Application.Common.Exceptions;
using MaintenanceBoard.Application.Tickets;
using MaintenanceBoard.Application.Tickets.Exceptions;
using MaintenanceBoard.Domain.Access;
using MaintenanceBoard.Domain.Tickets;
using MaintenanceBoard.Tests.Fakes;

namespace MaintenanceBoard.Tests.Tickets;

public sealed class TicketTransitionServiceTests
{
    [Theory]
    [InlineData(UserRole.Operator)]
    [InlineData(UserRole.Supervisor)]
    public async Task Operators_and_supervisors_can_start_attention_on_any_ticket(UserRole role)
    {
        var context = new TicketServiceContext(role);
        context.Tickets.Assignments.Add(ApplicationTestData.Assignment(30));

        await context.Service.TransitionAsync(100, new TransitionTicketRequest("Pending", "InProgress"));

        var call = Assert.IsType<FakeTicketRepository.TransitionCall>(context.Tickets.Transitioned);
        Assert.Equal(TicketStatus.Pending, call.ExpectedStatus);
        Assert.Equal(TicketStatus.InProgress, call.NewStatus);
        Assert.Equal(10, call.ActorUserId);
        Assert.Equal(1, context.Tickets.WriteCalls);
    }

    [Fact]
    public async Task Requesters_cannot_change_status_even_on_their_own_ticket()
    {
        var context = new TicketServiceContext(UserRole.Requester);
        context.Tickets.Ticket = ApplicationTestData.Ticket(reportedByUserId: 10);

        await Assert.ThrowsAsync<ForbiddenOperationException>(() =>
            context.Service.TransitionAsync(100, new TransitionTicketRequest("Pending", "Rejected", "Duplicate.")));

        Assert.Equal(0, context.Tickets.ReadCalls);
        Assert.Equal(0, context.Tickets.WriteCalls);
    }

    [Theory]
    [InlineData("Pending", "Closed")]
    [InlineData("Pending", "Pending")]
    [InlineData("InProgress", "Pending")]
    [InlineData("InProgress", "InProgress")]
    [InlineData("Closed", "Pending")]
    [InlineData("Closed", "Rejected")]
    [InlineData("Rejected", "InProgress")]
    [InlineData("Rejected", "Closed")]
    [InlineData("Pending", "Unknown")]
    [InlineData("Pending", "inprogress")]
    [InlineData("Pending", "InProgress ")]
    public async Task Domain_transition_failures_become_application_failures_without_writing(string currentStatus, string newStatus)
    {
        var context = new TicketServiceContext();
        context.Tickets.Ticket = ApplicationTestData.Ticket(Enum.Parse<TicketStatus>(currentStatus));
        context.Tickets.Assignments.Add(ApplicationTestData.Assignment(30));

        var exception = await Assert.ThrowsAsync<InvalidTicketTransitionException>(() =>
            context.Service.TransitionAsync(100, new TransitionTicketRequest(currentStatus, newStatus)));

        Assert.IsType<TicketTransitionException>(exception.InnerException);
        Assert.Equal("ticket.invalid_transition", exception.Code);
        Assert.Equal(0, context.Tickets.WriteCalls);
    }

    [Fact]
    public async Task Starting_without_an_assignee_fails_before_persistence()
    {
        var context = new TicketServiceContext();

        var exception = await Assert.ThrowsAsync<InvalidTicketTransitionException>(() =>
            context.Service.TransitionAsync(100, new TransitionTicketRequest("Pending", "InProgress")));

        Assert.Contains("at least one operator", exception.Message);
        Assert.Equal(0, context.Tickets.WriteCalls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" \t\u3000 ")]
    public async Task Closing_without_a_cause_returns_the_application_cause_failure(string? cause)
    {
        var context = new TicketServiceContext();
        context.Tickets.Ticket = ApplicationTestData.Ticket(TicketStatus.InProgress);

        await Assert.ThrowsAsync<TicketCauseRequiredException>(() =>
            context.Service.TransitionAsync(100, new TransitionTicketRequest("InProgress", "Closed", Cause: cause)));

        Assert.Equal(0, context.Tickets.WriteCalls);
    }

    [Fact]
    public async Task Closing_passes_the_cause_to_persistence_and_uses_the_authenticated_actor()
    {
        var context = new TicketServiceContext();
        context.Tickets.Ticket = ApplicationTestData.Ticket(TicketStatus.InProgress);

        await context.Service.TransitionAsync(100,
            new TransitionTicketRequest("InProgress", "Closed", Cause: "  Power supply failure.  "));

        var call = Assert.IsType<FakeTicketRepository.TransitionCall>(context.Tickets.Transitioned);
        Assert.Equal("Power supply failure.", call.Cause);
        Assert.Null(call.Comment);
        Assert.Equal(10, call.ActorUserId);
    }

    [Theory]
    [InlineData(TicketStatus.Pending)]
    [InlineData(TicketStatus.InProgress)]
    public async Task Rejection_requires_a_reason_in_both_open_states(TicketStatus status)
    {
        var context = new TicketServiceContext();
        context.Tickets.Ticket = ApplicationTestData.Ticket(status);

        await Assert.ThrowsAsync<TicketRejectionReasonRequiredException>(() =>
            context.Service.TransitionAsync(100, new TransitionTicketRequest(status.ToString(), "Rejected", " \t ")));

        Assert.Equal(0, context.Tickets.WriteCalls);
    }

    [Theory]
    [InlineData(TicketStatus.Pending)]
    [InlineData(TicketStatus.InProgress)]
    public async Task Rejection_with_a_reason_does_not_require_an_assignee_or_cause(TicketStatus status)
    {
        var context = new TicketServiceContext();
        context.Tickets.Ticket = ApplicationTestData.Ticket(status);

        await context.Service.TransitionAsync(100,
            new TransitionTicketRequest(status.ToString(), "Rejected", "  Duplicate incident.  "));

        var call = Assert.IsType<FakeTicketRepository.TransitionCall>(context.Tickets.Transitioned);
        Assert.Equal("Duplicate incident.", call.Comment);
        Assert.Null(call.Cause);
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("inprogress")]
    [InlineData(" InProgress")]
    public async Task Stale_or_inexact_expected_status_fails_before_loading_assignments(string expectedStatus)
    {
        var context = new TicketServiceContext();
        context.Tickets.Ticket = ApplicationTestData.Ticket(TicketStatus.InProgress);

        await Assert.ThrowsAsync<TicketConcurrencyException>(() =>
            context.Service.TransitionAsync(100, new TransitionTicketRequest(expectedStatus, "Closed", Cause: "Cause.")));

        Assert.Equal(1, context.Tickets.ReadCalls);
        Assert.Equal(0, context.Tickets.WriteCalls);
    }

    [Fact]
    public async Task A_concurrent_change_detected_by_persistence_is_not_swallowed()
    {
        var context = new TicketServiceContext();
        context.Tickets.Assignments.Add(ApplicationTestData.Assignment(30));
        var conflict = new TicketConcurrencyException();
        context.Tickets.WriteException = conflict;

        var exception = await Assert.ThrowsAsync<TicketConcurrencyException>(() =>
            context.Service.TransitionAsync(100, new TransitionTicketRequest("Pending", "InProgress")));

        Assert.Same(conflict, exception);
    }

    [Fact]
    public async Task Transitioning_a_missing_ticket_fails_before_writing()
    {
        var context = new TicketServiceContext();

        await Assert.ThrowsAsync<TicketNotFoundException>(() =>
            context.Service.TransitionAsync(999, new TransitionTicketRequest("Pending", "InProgress")));

        Assert.Equal(0, context.Tickets.WriteCalls);
    }

    [Theory]
    [InlineData(null, "InProgress")]
    [InlineData("Pending", null)]
    [InlineData("Pending", " ")]
    public async Task Missing_status_fields_are_validation_failures(string? expectedStatus, string? newStatus)
    {
        var context = new TicketServiceContext();

        await Assert.ThrowsAsync<RequestValidationException>(() =>
            context.Service.TransitionAsync(100, new TransitionTicketRequest(expectedStatus, newStatus)));

        Assert.Equal(0, context.Tickets.WriteCalls);
    }

    [Fact]
    public async Task Cancellation_tokens_reach_both_repositories()
    {
        var context = new TicketServiceContext();
        context.Tickets.Assignments.Add(ApplicationTestData.Assignment(30));
        using var source = new CancellationTokenSource();

        await context.Service.TransitionAsync(100, new TransitionTicketRequest("Pending", "InProgress"), source.Token);

        Assert.Equal(source.Token, context.Users.LastCancellationToken);
        Assert.Equal(source.Token, context.Tickets.LastCancellationToken);
    }
}
