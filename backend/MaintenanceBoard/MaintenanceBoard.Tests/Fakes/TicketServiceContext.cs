using MaintenanceBoard.Application.Tickets;
using MaintenanceBoard.Domain.Access;
using MaintenanceBoard.Domain.Tickets;

namespace MaintenanceBoard.Tests.Fakes;

internal sealed class TicketServiceContext
{
    public FakeCurrentUser CurrentUser { get; } = new();
    public FakeUserRepository Users { get; } = new();
    public FakeTicketRepository Tickets { get; } = new();
    public TicketService Service { get; }

    public TicketServiceContext(UserRole actorRole = UserRole.Operator)
    {
        foreach (var user in new[]
        {
            ApplicationTestData.User(10, actorRole),
            ApplicationTestData.User(20, UserRole.Requester),
            ApplicationTestData.User(30, UserRole.Operator),
            ApplicationTestData.User(40, UserRole.Supervisor),
            ApplicationTestData.User(60, UserRole.Operator),
            ApplicationTestData.User(70, UserRole.Requester),
            ApplicationTestData.User(80, UserRole.Operator, isActive: false)
        })
        {
            Users.Users.Add(user.UserId, user);
        }

        Service = new TicketService(Tickets, Users, CurrentUser, new TicketTransitionPolicy());
    }
}
