using MaintenanceBoard.Application.Common.Exceptions;
using MaintenanceBoard.Application.Tickets;
using MaintenanceBoard.Application.Tickets.Exceptions;
using MaintenanceBoard.Domain.Access;
using MaintenanceBoard.Domain.Tickets;
using MaintenanceBoard.Tests.Fakes;

namespace MaintenanceBoard.Tests.Tickets;

public sealed class TicketQueryServiceTests
{
    [Theory]
    [InlineData(UserRole.Requester)]
    [InlineData(UserRole.Operator)]
    [InlineData(UserRole.Supervisor)]
    public async Task The_main_board_contains_only_the_three_visible_states(UserRole role)
    {
        var context = new TicketServiceContext(role);
        context.Tickets.BoardItems = [
            ApplicationTestData.ListItem(TicketStatus.Pending, 1),
            ApplicationTestData.ListItem(TicketStatus.InProgress, 2),
            ApplicationTestData.ListItem(TicketStatus.Closed, 3),
            ApplicationTestData.ListItem(TicketStatus.Rejected, 4)];

        var board = await context.Service.GetBoardAsync();

        Assert.Equal(1, Assert.Single(board.Pending).TicketId);
        Assert.Equal(2, Assert.Single(board.InProgress).TicketId);
        Assert.Equal(3, Assert.Single(board.Closed).TicketId);
    }

    [Fact]
    public async Task Board_order_is_newest_first_with_a_deterministic_id_tie_breaker()
    {
        var context = new TicketServiceContext();
        var older = ApplicationTestData.ListItem(TicketStatus.Pending, 1) with
        {
            CreatedAtUtc = ApplicationTestData.Timestamp.AddDays(-1)
        };
        context.Tickets.BoardItems = [older,
            ApplicationTestData.ListItem(TicketStatus.Pending, 2),
            ApplicationTestData.ListItem(TicketStatus.Pending, 3)];

        var board = await context.Service.GetBoardAsync();

        Assert.Equal(new[] { 3, 2, 1 }, board.Pending.Select(item => item.TicketId));
    }

    [Fact]
    public async Task The_general_listing_can_include_rejected_tickets()
    {
        var context = new TicketServiceContext();
        context.Tickets.ListItems = [ApplicationTestData.ListItem(TicketStatus.Rejected)];

        var items = await context.Service.GetListAsync(new TicketListRequest("Rejected"));

        Assert.Equal(TicketStatus.Rejected, Assert.Single(items).Status);
        Assert.Equal(TicketStatus.Rejected, context.Tickets.LastFilter!.Status);
    }

    [Fact]
    public async Task Date_filters_are_converted_to_UTC_before_repository_access()
    {
        var context = new TicketServiceContext();
        var from = new DateTimeOffset(2026, 11, 8, 0, 0, 0, TimeSpan.FromHours(-5));
        var to = from.AddDays(1);

        await context.Service.GetListAsync(new TicketListRequest("Pending", from, to));

        var filter = Assert.IsType<TicketQueryFilter>(context.Tickets.LastFilter);
        Assert.Equal(new DateTime(2026, 11, 8, 5, 0, 0, DateTimeKind.Utc), filter.CreatedFromUtc);
        Assert.Equal(new DateTime(2026, 11, 9, 5, 0, 0, DateTimeKind.Utc), filter.CreatedToUtc);
    }

    [Fact]
    public async Task Missing_filters_remain_optional()
    {
        var context = new TicketServiceContext();

        await context.Service.GetListAsync(new TicketListRequest());

        var filter = Assert.IsType<TicketQueryFilter>(context.Tickets.LastFilter);
        Assert.Null(filter.Status);
        Assert.Null(filter.CreatedFromUtc);
        Assert.Null(filter.CreatedToUtc);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Unknown")]
    [InlineData("pending")]
    [InlineData("Pending ")]
    [InlineData("0")]
    public async Task Invalid_status_filters_fail_before_querying_tickets(string status)
    {
        var context = new TicketServiceContext();

        await Assert.ThrowsAsync<RequestValidationException>(() =>
            context.Service.GetListAsync(new TicketListRequest(status)));

        Assert.Equal(0, context.Tickets.ReadCalls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Equal_or_reversed_date_ranges_are_rejected(int dayOffset)
    {
        var context = new TicketServiceContext();
        var from = new DateTimeOffset(ApplicationTestData.Timestamp);

        await Assert.ThrowsAsync<RequestValidationException>(() =>
            context.Service.GetListAsync(new TicketListRequest(From: from, To: from.AddDays(dayOffset))));

        Assert.Equal(0, context.Tickets.ReadCalls);
    }

    [Fact]
    public async Task Details_include_the_full_history_in_chronological_order()
    {
        var context = new TicketServiceContext();
        context.Tickets.Details = new TicketDetails(
            ApplicationTestData.ListItem(TicketStatus.InProgress), "Known cause.",
            [ApplicationTestData.History(3), ApplicationTestData.History(2),
             ApplicationTestData.History(1, ApplicationTestData.Timestamp.AddHours(-1))]);

        var details = await context.Service.GetDetailsAsync(100);

        Assert.Equal(new long[] { 1, 2, 3 }, details.History.Select(item => item.HistoryId));
        Assert.Equal("Known cause.", details.Cause);
    }

    [Fact]
    public async Task Missing_details_return_a_not_found_failure()
    {
        var context = new TicketServiceContext();

        await Assert.ThrowsAsync<TicketNotFoundException>(() => context.Service.GetDetailsAsync(999));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Invalid_ticket_ids_are_validation_failures(int ticketId)
    {
        var context = new TicketServiceContext();

        await Assert.ThrowsAsync<RequestValidationException>(() => context.Service.GetDetailsAsync(ticketId));

        Assert.Equal(0, context.Tickets.ReadCalls);
    }

    [Fact]
    public async Task Unauthenticated_users_cannot_read_the_board()
    {
        var context = new TicketServiceContext();
        context.CurrentUser.UserId = null;

        await Assert.ThrowsAsync<UnauthenticatedUserException>(() => context.Service.GetBoardAsync());

        Assert.Equal(0, context.Tickets.ReadCalls);
    }
}
