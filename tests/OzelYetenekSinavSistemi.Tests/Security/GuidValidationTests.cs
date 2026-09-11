namespace OzelYetenekSinavSistemi.Tests.Security;

/// <summary>
/// Kullanıcı kaynaklı Guid değerlerinin güvenli doğrulanması (TryParse) davranışı.
/// </summary>
public sealed class GuidValidationTests
{
    [Theory]
    [InlineData("11111111-1111-1111-1111-111111111111")]
    [InlineData("22222222-2222-2222-2222-222222222222")]
    public void TryParse_ValidGuid_ReturnsTrue(string value)
    {
        Assert.True(Guid.TryParse(value, out var parsed));
        Assert.NotEqual(Guid.Empty, parsed);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("123")]
    [InlineData("")]
    [InlineData("11111111-1111-1111-1111-11111111111Z")]
    public void TryParse_InvalidGuid_ReturnsFalse(string value)
    {
        Assert.False(Guid.TryParse(value, out _));
    }
}
