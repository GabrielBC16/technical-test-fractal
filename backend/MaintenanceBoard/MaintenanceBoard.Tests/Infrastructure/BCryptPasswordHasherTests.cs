using MaintenanceBoard.Infrastructure.Access;

namespace MaintenanceBoard.Tests.Infrastructure;

public sealed class BCryptPasswordHasherTests
{
    private readonly BCryptPasswordHasher _hasher = new(workFactor: 4);

    [Fact]
    public void Hashes_are_salted_and_verify_only_the_correct_password()
    {
        const string password = "validation-only-password";

        var first = _hasher.Hash(password);
        var second = _hasher.Hash(password);

        Assert.NotEqual(password, first);
        Assert.NotEqual(first, second);
        Assert.Equal(60, first.Length);
        Assert.True(_hasher.Verify(password, first));
        Assert.False(_hasher.Verify("incorrect-password", first));
    }

    [Fact]
    public void Password_whitespace_is_significant()
    {
        var hash = _hasher.Hash("  validation password  ");

        Assert.True(_hasher.Verify("  validation password  ", hash));
        Assert.False(_hasher.Verify("validation password", hash));
    }

    [Fact]
    public void Passwords_beyond_72_UTF8_bytes_cannot_match_through_truncation()
    {
        var password = new string('a', 72);
        var hash = _hasher.Hash(password);

        Assert.True(_hasher.Verify(password, hash));
        Assert.False(_hasher.Verify(password + "b", hash));
        Assert.Throws<ArgumentException>(() => _hasher.Hash(password + "b"));
    }

    [Fact]
    public void The_password_limit_counts_UTF8_bytes_instead_of_characters()
    {
        var password = new string('é', 36);
        var hash = _hasher.Hash(password);

        Assert.True(_hasher.Verify(password, hash));
        Assert.False(_hasher.Verify(password + "é", hash));
        Assert.Throws<ArgumentException>(() => _hasher.Hash(password + "é"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Empty_hash_inputs_are_rejected(string password)
    {
        Assert.Throws<ArgumentException>(() => _hasher.Hash(password));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("$2a$31$.....................................................")]
    [InlineData("$2a$03$.....................................................")]
    [InlineData("$2a$11$short")]
    public void Invalid_or_excessive_cost_hashes_fail_verification_without_throwing(string hash)
    {
        Assert.False(_hasher.Verify("validation-password", hash));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(17)]
    public void Unsupported_work_factors_are_rejected(int workFactor)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BCryptPasswordHasher(workFactor));
    }
}
