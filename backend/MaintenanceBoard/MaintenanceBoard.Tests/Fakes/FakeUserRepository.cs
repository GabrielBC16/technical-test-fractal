using MaintenanceBoard.Application.Access;
using MaintenanceBoard.Domain.Access;

namespace MaintenanceBoard.Tests.Fakes;

internal sealed class FakeUserRepository : IUserRepository
{
    public Dictionary<int, AppUser> Users { get; } = [];
    public Dictionary<string, UserCredentials> Credentials { get; } = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<AppUser> Candidates { get; set; } = [];
    public string? LastUserName { get; private set; }
    public int LookupCalls { get; private set; }
    public int CandidateCalls { get; private set; }
    public CancellationToken LastCancellationToken { get; private set; }

    public Task<AppUser?> GetByIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        Observe(cancellationToken);
        LookupCalls++;
        return Task.FromResult(Users.GetValueOrDefault(userId));
    }

    public Task<UserCredentials?> GetCredentialsByUserNameAsync(string userName, CancellationToken cancellationToken = default)
    {
        Observe(cancellationToken);
        LastUserName = userName;
        return Task.FromResult(Credentials.GetValueOrDefault(userName));
    }

    public Task<IReadOnlyList<AppUser>> GetActiveAssignableUsersAsync(CancellationToken cancellationToken = default)
    {
        Observe(cancellationToken);
        CandidateCalls++;
        return Task.FromResult(Candidates);
    }

    private void Observe(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastCancellationToken = cancellationToken;
    }
}
