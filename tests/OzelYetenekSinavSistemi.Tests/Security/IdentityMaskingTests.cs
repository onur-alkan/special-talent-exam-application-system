using OzelYetenekSinavSistemi.Application.Services;
using OzelYetenekSinavSistemi.Domain.Enums;

namespace OzelYetenekSinavSistemi.Tests.Security;

public sealed class IdentityMaskingTests
{
    private readonly SensitiveDataMaskingService _sut = new();

    [Fact]
    public void MaskIdentityNumber_TurkishIdentity_PreservesTcMaskFormat()
    {
        var masked = _sut.MaskIdentityNumber(
            IdentityDocumentType.TurkishIdentityNumber,
            "12345678901",
            null);

        Assert.Equal("123******01", masked);
        Assert.DoesNotContain("45678", masked);
    }

    [Fact]
    public void MaskIdentityNumber_ForeignIdentity_ShowsFirstTwoAndLastTwo()
    {
        var masked = _sut.MaskIdentityNumber(
            IdentityDocumentType.ForeignIdentityNumber,
            "99000000001",
            null);

        Assert.Equal("99*******01", masked);
        Assert.DoesNotContain("99000000001", masked);
    }

    [Fact]
    public void MaskIdentityNumber_Passport_ShowsFirstTwoAndLastTwo()
    {
        var masked = _sut.MaskIdentityNumber(
            IdentityDocumentType.Passport,
            "AB1234567",
            null);

        Assert.Equal("AB*****67", masked);
        Assert.DoesNotContain("AB1234567", masked, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("AB")]
    [InlineData("A")]
    [InlineData(null)]
    [InlineData("")]
    public void MaskIdentityNumber_ShortOrEmptyValues_AreHandledSafely(string? value)
    {
        var masked = _sut.MaskIdentityNumber(IdentityDocumentType.Passport, value, null);
        Assert.False(string.IsNullOrEmpty(masked) && !string.IsNullOrEmpty(value));
        if (string.IsNullOrWhiteSpace(value))
            Assert.Equal(SensitiveDataMaskingService.EmptyIdentityDisplay, masked);
    }

    [Fact]
    public void MaskIdentityNumber_LegacyTcNoFallback_UsesTurkishMask()
    {
        var masked = _sut.MaskIdentityNumber(null, null, "10000000146");
        Assert.Equal(_sut.MaskTcNo("10000000146"), masked);
        Assert.DoesNotContain("10000000146", masked);
    }

    [Fact]
    public void MaskTcNo_RemainsWrapperForTurkishMask()
    {
        Assert.Equal(
            _sut.MaskIdentityNumber(IdentityDocumentType.TurkishIdentityNumber, "12345678901", null),
            _sut.MaskTcNo("12345678901"));
    }
}
