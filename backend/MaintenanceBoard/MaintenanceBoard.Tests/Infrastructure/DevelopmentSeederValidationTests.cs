using MaintenanceBoard.Domain.Access;
using MaintenanceBoard.Infrastructure.Development;
using MaintenanceBoard.Infrastructure.Persistence;
using MaintenanceBoard.Tests.Fakes;
using Microsoft.Data.SqlClient;

namespace MaintenanceBoard.Tests.Infrastructure;

public sealed class DevelopmentSeederValidationTests
{
    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("")]
    public async Task Seeding_never_opens_a_database_outside_Development(string environment)
    {
        var connections = new RejectingConnectionFactory();
        var seeder = new DevelopmentSeeder(connections, new FakePasswordHasher());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            seeder.SeedAsync(environment, new DevelopmentSeedOptions([], IncludeSampleTickets: false)));

        Assert.Equal(0, connections.OpenCalls);
    }

    [Fact]
    public async Task Empty_user_configuration_is_rejected_before_opening_a_database()
    {
        await AssertInvalidAsync(new DevelopmentSeedOptions([], IncludeSampleTickets: false));
    }

    [Fact]
    public async Task Duplicate_user_names_are_rejected_before_opening_a_database()
    {
        var user = User();
        await AssertInvalidAsync(new DevelopmentSeedOptions(
            [user, user with { UserName = " USER " }], IncludeSampleTickets: false));
    }

    [Fact]
    public async Task Passwords_and_field_lengths_are_validated_before_opening_a_database()
    {
        foreach (var user in new[]
        {
            User() with { Password = "" },
            User() with { Password = new string('x', 73) },
            User() with { UserName = "" },
            User() with { UserName = new string('x', 101) },
            User() with { Names = "" },
            User() with { Names = new string('x', 101) },
            User() with { LastNames = "" },
            User() with { LastNames = new string('x', 151) },
            User() with { Role = (UserRole)99 }
        })
        {
            await AssertInvalidAsync(new DevelopmentSeedOptions([user], IncludeSampleTickets: false));
        }
    }

    [Theory]
    [InlineData(UserRole.Requester)]
    [InlineData(UserRole.Operator)]
    [InlineData(UserRole.Supervisor)]
    public async Task Sample_tickets_require_a_requester_and_an_eligible_operator(UserRole role)
    {
        await AssertInvalidAsync(new DevelopmentSeedOptions([User() with { Role = role }]));
    }

    private static DevelopmentSeedUser User() => new(
        "user", "Test", "User", UserRole.Requester, "validation-only-password");

    private static async Task AssertInvalidAsync(DevelopmentSeedOptions options)
    {
        var connections = new RejectingConnectionFactory();
        var seeder = new DevelopmentSeeder(connections, new FakePasswordHasher());

        await Assert.ThrowsAsync<InvalidOperationException>(() => seeder.SeedAsync("Development", options));

        Assert.Equal(0, connections.OpenCalls);
    }

    private sealed class RejectingConnectionFactory : ISqlConnectionFactory
    {
        public int OpenCalls { get; private set; }

        public Task<SqlConnection> OpenAsync(CancellationToken cancellationToken = default)
        {
            OpenCalls++;
            throw new InvalidOperationException("The validation test must not open a database.");
        }
    }
}
