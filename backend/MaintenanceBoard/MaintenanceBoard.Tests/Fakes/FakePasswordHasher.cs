using MaintenanceBoard.Application.Access;

namespace MaintenanceBoard.Tests.Fakes;

internal sealed class FakePasswordHasher : IPasswordHasher
{
    public string ExpectedPassword { get; set; } = "correct password";
    public string ExpectedHash { get; set; } = "validation-only-hash";
    public string? LastPassword { get; private set; }
    public int VerifyCalls { get; private set; }

    public string Hash(string password) => "test-only:" + password;

    public bool Verify(string password, string passwordHash)
    {
        LastPassword = password;
        VerifyCalls++;
        return string.Equals(password, ExpectedPassword, StringComparison.Ordinal) &&
            string.Equals(passwordHash, ExpectedHash, StringComparison.Ordinal);
    }
}
