using System.Text;
using System.Text.RegularExpressions;
using MaintenanceBoard.Application.Access;
using BC = BCrypt.Net.BCrypt;

namespace MaintenanceBoard.Infrastructure.Access;

public sealed partial class BCryptPasswordHasher : IPasswordHasher
{
    private readonly int _workFactor;

    public BCryptPasswordHasher(int workFactor = 11)
    {
        if (workFactor is < 4 or > 16)
        {
            throw new ArgumentOutOfRangeException(nameof(workFactor), "The BCrypt work factor must be between 4 and 16.");
        }

        _workFactor = workFactor;
    }

    public string Hash(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || Encoding.UTF8.GetByteCount(password) > 72)
        {
            throw new ArgumentException("A password must contain text and fit within 72 UTF-8 bytes.", nameof(password));
        }

        return BC.HashPassword(password, workFactor: _workFactor);
    }

    public bool Verify(string password, string passwordHash)
    {
        // Reject overlong inputs rather than accepting BCrypt's 72-byte truncation.
        if (string.IsNullOrEmpty(password) || Encoding.UTF8.GetByteCount(password) > 72 ||
            string.IsNullOrEmpty(passwordHash) || !SupportedHash().IsMatch(passwordHash))
        {
            return false;
        }

        try
        {
            return BC.Verify(password, passwordHash);
        }
        catch (Exception exception) when (exception is BCrypt.Net.SaltParseException or ArgumentException or FormatException)
        {
            return false;
        }
    }

    [GeneratedRegex(@"\A\$2[aby]\$(0[4-9]|1[0-6])\$[./A-Za-z0-9]{53}\z", RegexOptions.CultureInvariant)]
    private static partial Regex SupportedHash();
}
