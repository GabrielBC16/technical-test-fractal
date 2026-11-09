using MaintenanceBoard.Application.Access;
using MaintenanceBoard.Application.Common.Exceptions;
using MaintenanceBoard.Domain.Access;
using MaintenanceBoard.Tests.Fakes;

namespace MaintenanceBoard.Tests.Access;

public sealed class AuthServiceTests
{
    [Theory]
    [InlineData(UserRole.Requester)]
    [InlineData(UserRole.Operator)]
    [InlineData(UserRole.Supervisor)]
    public async Task Login_verifies_the_hash_and_returns_the_active_users_identity(UserRole role)
    {
        var users = new FakeUserRepository();
        var hasher = new FakePasswordHasher();
        var user = ApplicationTestData.User(role: role);
        users.Credentials.Add(user.UserName, new UserCredentials(user, hasher.ExpectedHash));
        var service = new AuthService(users, hasher);

        var response = await service.LoginAsync(new LoginRequest("  " + user.UserName + "  ", hasher.ExpectedPassword));

        Assert.Equal(user.UserId, response.UserId);
        Assert.Equal(user.Names, response.Names);
        Assert.Equal(user.LastNames, response.LastNames);
        Assert.Equal(role, response.Role);
        Assert.Equal(user.UserName, users.LastUserName);
        Assert.Equal(1, hasher.VerifyCalls);
    }

    [Fact]
    public async Task Password_whitespace_is_preserved_when_verifying_the_hash()
    {
        var users = new FakeUserRepository();
        var hasher = new FakePasswordHasher { ExpectedPassword = "  secret password  " };
        var user = ApplicationTestData.User();
        users.Credentials.Add(user.UserName, new UserCredentials(user, hasher.ExpectedHash));
        var service = new AuthService(users, hasher);

        await service.LoginAsync(new LoginRequest(user.UserName, hasher.ExpectedPassword));

        Assert.Equal("  secret password  ", hasher.LastPassword);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("inactive")]
    [InlineData("wrong-password")]
    [InlineData("empty-hash")]
    [InlineData("unknown-role")]
    public async Task Invalid_credentials_always_return_the_same_authentication_failure(string scenario)
    {
        var users = new FakeUserRepository();
        var hasher = new FakePasswordHasher();
        var user = ApplicationTestData.User();
        if (scenario == "inactive") user = user with { IsActive = false };
        if (scenario == "unknown-role") user = user with { Role = (UserRole)99 };
        if (scenario != "missing")
        {
            users.Credentials.Add(user.UserName,
                new UserCredentials(user, scenario == "empty-hash" ? "" : hasher.ExpectedHash));
        }
        var service = new AuthService(users, hasher);

        var exception = await Assert.ThrowsAsync<UnauthenticatedUserException>(() =>
            service.LoginAsync(new LoginRequest(user.UserName,
                scenario == "wrong-password" ? "incorrect password" : hasher.ExpectedPassword)));

        Assert.Equal("Invalid user name or password.", exception.Message);
    }

    [Theory]
    [InlineData(null, "password")]
    [InlineData(" ", "password")]
    [InlineData("user", null)]
    [InlineData("user", "")]
    public async Task Login_requires_user_name_and_password(string? userName, string? password)
    {
        var users = new FakeUserRepository();
        var hasher = new FakePasswordHasher();
        var service = new AuthService(users, hasher);

        await Assert.ThrowsAsync<RequestValidationException>(() => service.LoginAsync(new LoginRequest(userName, password)));

        Assert.Equal(0, hasher.VerifyCalls);
        Assert.Null(users.LastUserName);
    }

    [Fact]
    public async Task Missing_users_are_not_passed_to_the_password_verifier()
    {
        var users = new FakeUserRepository();
        var hasher = new FakePasswordHasher();

        await Assert.ThrowsAsync<UnauthenticatedUserException>(() =>
            new AuthService(users, hasher).LoginAsync(new LoginRequest("missing", "password")));

        Assert.Equal(0, hasher.VerifyCalls);
    }
}
