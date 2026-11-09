using MaintenanceBoard.Application.Common.Exceptions;
using MaintenanceBoard.Application.Tickets;
using MaintenanceBoard.Application.Tickets.Exceptions;
using MaintenanceBoard.Domain.Access;
using MaintenanceBoard.Domain.Tickets;
using MaintenanceBoard.Tests.Fakes;

namespace MaintenanceBoard.Tests.Tickets;

public sealed class TicketAssignmentServiceTests
{
    [Theory]
    [InlineData(UserRole.Operator)]
    [InlineData(UserRole.Supervisor)]
    public async Task Operators_and_supervisors_can_assign_themselves_without_a_first_justification(UserRole role)
    {
        var context = new TicketServiceContext(role);

        await context.Service.AssignAsync(100, new AssignTicketRequest(10));

        var call = Assert.IsType<FakeTicketRepository.AssignmentCall>(context.Tickets.Assigned);
        Assert.Equal(10, call.AssignedUserId);
        Assert.Equal(10, call.ActorUserId);
        Assert.Null(call.Justification);
    }

    [Fact]
    public async Task Requesters_cannot_assign_tickets()
    {
        var context = new TicketServiceContext(UserRole.Requester);

        await Assert.ThrowsAsync<ForbiddenOperationException>(() =>
            context.Service.AssignAsync(100, new AssignTicketRequest(30)));

        Assert.Equal(0, context.Tickets.ReadCalls);
        Assert.Equal(0, context.Tickets.WriteCalls);
    }

    [Theory]
    [InlineData(TicketStatus.Pending)]
    [InlineData(TicketStatus.InProgress)]
    public async Task Assignment_is_allowed_on_both_open_states(TicketStatus status)
    {
        var context = new TicketServiceContext();
        context.Tickets.Ticket = ApplicationTestData.Ticket(status);

        await context.Service.AssignAsync(100, new AssignTicketRequest(40));

        var call = Assert.IsType<FakeTicketRepository.AssignmentCall>(context.Tickets.Assigned);
        Assert.Equal(40, call.AssignedUserId);
        Assert.Equal(10, call.ActorUserId);
    }

    [Theory]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Rejected)]
    public async Task Terminal_tickets_cannot_receive_assignments(TicketStatus status)
    {
        var context = new TicketServiceContext();
        context.Tickets.Ticket = ApplicationTestData.Ticket(status);

        await Assert.ThrowsAsync<TicketAssignmentConflictException>(() =>
            context.Service.AssignAsync(100, new AssignTicketRequest(30)));

        Assert.Equal(0, context.Tickets.WriteCalls);
    }

    [Theory]
    [InlineData(999)]
    [InlineData(80)]
    [InlineData(20)]
    public async Task Missing_inactive_or_requester_assignees_are_rejected(int assignedUserId)
    {
        var context = new TicketServiceContext();

        await Assert.ThrowsAsync<InvalidTicketAssigneeException>(() =>
            context.Service.AssignAsync(100, new AssignTicketRequest(assignedUserId)));

        Assert.Equal(0, context.Tickets.WriteCalls);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Additional_assignees_require_a_justification(int existingCount)
    {
        var context = new TicketServiceContext();
        context.Tickets.Assignments.Add(ApplicationTestData.Assignment(30));
        if (existingCount == 2)
        {
            context.Tickets.Assignments.Add(ApplicationTestData.Assignment(40));
        }

        await Assert.ThrowsAsync<TicketAssignmentJustificationRequiredException>(() =>
            context.Service.AssignAsync(100, new AssignTicketRequest(60, " \t\u00A0 ")));

        Assert.Equal(0, context.Tickets.WriteCalls);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Additional_assignees_are_allowed_with_a_justification(int existingCount)
    {
        var context = new TicketServiceContext();
        context.Tickets.Assignments.Add(ApplicationTestData.Assignment(30));
        if (existingCount == 2)
        {
            context.Tickets.Assignments.Add(ApplicationTestData.Assignment(40));
        }

        await context.Service.AssignAsync(100, new AssignTicketRequest(60, "  Specialist review required.  "));

        var call = Assert.IsType<FakeTicketRepository.AssignmentCall>(context.Tickets.Assigned);
        Assert.Equal("Specialist review required.", call.Justification);
    }

    [Fact]
    public async Task A_fourth_assignee_is_rejected()
    {
        var context = new TicketServiceContext();
        context.Tickets.Assignments.AddRange([
            ApplicationTestData.Assignment(10),
            ApplicationTestData.Assignment(30),
            ApplicationTestData.Assignment(40)]);

        await Assert.ThrowsAsync<TicketAssignmentLimitException>(() =>
            context.Service.AssignAsync(100, new AssignTicketRequest(60, "Specialist review.")));

        Assert.Equal(0, context.Tickets.WriteCalls);
    }

    [Fact]
    public async Task Duplicates_take_precedence_over_the_assignment_limit()
    {
        var context = new TicketServiceContext();
        context.Tickets.Assignments.AddRange([
            ApplicationTestData.Assignment(10),
            ApplicationTestData.Assignment(30),
            ApplicationTestData.Assignment(40)]);

        await Assert.ThrowsAsync<TicketAssignmentConflictException>(() =>
            context.Service.AssignAsync(100, new AssignTicketRequest(30, "Specialist review.")));

        Assert.Equal(0, context.Tickets.WriteCalls);
    }

    [Fact]
    public async Task Oversized_justifications_fail_before_writing()
    {
        var context = new TicketServiceContext();

        await Assert.ThrowsAsync<RequestValidationException>(() =>
            context.Service.AssignAsync(100, new AssignTicketRequest(30, new string('x', 2001))));

        Assert.Equal(0, context.Tickets.WriteCalls);
    }

    [Fact]
    public async Task Missing_tickets_fail_before_assignment()
    {
        var context = new TicketServiceContext();

        await Assert.ThrowsAsync<TicketNotFoundException>(() =>
            context.Service.AssignAsync(999, new AssignTicketRequest(30)));

        Assert.Equal(0, context.Tickets.WriteCalls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Assignee_ids_must_be_positive(int assignedUserId)
    {
        var context = new TicketServiceContext();

        await Assert.ThrowsAsync<RequestValidationException>(() =>
            context.Service.AssignAsync(100, new AssignTicketRequest(assignedUserId)));

        Assert.Equal(0, context.Tickets.WriteCalls);
    }

    [Fact]
    public async Task Repository_conflicts_after_prevalidation_are_not_swallowed()
    {
        var context = new TicketServiceContext();
        var conflict = new TicketAssignmentLimitException();
        context.Tickets.WriteException = conflict;

        var exception = await Assert.ThrowsAsync<TicketAssignmentLimitException>(() =>
            context.Service.AssignAsync(100, new AssignTicketRequest(30)));

        Assert.Same(conflict, exception);
    }
}
