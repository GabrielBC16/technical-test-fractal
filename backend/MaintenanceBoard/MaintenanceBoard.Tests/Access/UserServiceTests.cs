using MaintenanceBoard.Application.Access;
using MaintenanceBoard.Application.Common.Exceptions;
using MaintenanceBoard.Domain.Access;
using MaintenanceBoard.Tests.Fakes;

namespace MaintenanceBoard.Tests.Access;

public sealed class UserServiceTests
{
    [Theory]
    [InlineData(UserRole.Operator)]
    [InlineData(UserRole.Supervisor)]
    public async Task Assignable_user_selection_excludes_requesters_and_inactive_users(UserRole role)
    {
        var users = new FakeUserRepository();
        users.Users.Add(10, ApplicationTestData.User(role: role));
        users.Candidates = [
            ApplicationTestData.User(40, UserRole.Supervisor),
            ApplicationTestData.User(20, UserRole.Requester),
            ApplicationTestData.User(30, UserRole.Operator),
            ApplicationTestData.User(80, UserRole.Operator, isActive: false)];
        var service = new UserService(users, new FakeCurrentUser());

        var result = await service.GetAssignableUsersAsync();

        Assert.Equal(new[] { 30, 40 }, result.Select(user => user.UserId));
        Assert.Equal(1, users.CandidateCalls);
    }

    [Fact]
    public async Task Requesters_cannot_list_users_for_assignment()
    {
        var users = new FakeUserRepository();
        users.Users.Add(10, ApplicationTestData.User(role: UserRole.Requester));

        await Assert.ThrowsAsync<ForbiddenOperationException>(() =>
            new UserService(users, new FakeCurrentUser()).GetAssignableUsersAsync());

        Assert.Equal(0, users.CandidateCalls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(999)]
    [InlineData(80)]
    public async Task Missing_and_inactive_actors_cannot_list_users(int? actorId)
    {
        var users = new FakeUserRepository();
        users.Users.Add(80, ApplicationTestData.User(80, isActive: false));

        await Assert.ThrowsAsync<UnauthenticatedUserException>(() =>
            new UserService(users, new FakeCurrentUser(actorId)).GetAssignableUsersAsync());

        Assert.Equal(0, users.CandidateCalls);
    }

    [Fact]
    public async Task Unknown_roles_are_not_authorized()
    {
        var users = new FakeUserRepository();
        users.Users.Add(10, ApplicationTestData.User(role: (UserRole)99));

        await Assert.ThrowsAsync<ForbiddenOperationException>(() =>
            new UserService(users, new FakeCurrentUser()).GetAssignableUsersAsync());

        Assert.Equal(0, users.CandidateCalls);
    }
}
