using OzelYetenekSinavSistemi.Infrastructure.Services;

namespace OzelYetenekSinavSistemi.Tests.Security;

public sealed class PasswordServiceTests
{
    private readonly PasswordService _sut = new();

    [Fact]
    public void Hash_ProducesNonPlaintextHash()
    {
        const string password = "S3curePass!";
        var hash = _sut.Hash(password);

        Assert.False(string.IsNullOrWhiteSpace(hash));
        Assert.NotEqual(password, hash);
    }

    [Fact]
    public void Verify_CorrectPassword_ReturnsTrue()
    {
        const string password = "S3curePass!";
        var hash = _sut.Hash(password);

        var ok = _sut.Verify(hash, password, out _);
        Assert.True(ok);
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        var hash = _sut.Hash("S3curePass!");

        var ok = _sut.Verify(hash, "WrongPass!", out _);
        Assert.False(ok);
    }

    [Fact]
    public void Verify_EmptyHash_ReturnsFalse()
    {
        var ok = _sut.Verify(string.Empty, "whatever", out _);
        Assert.False(ok);
    }
}
