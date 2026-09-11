using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Domain.Enums;

namespace OzelYetenekSinavSistemi.Tests.Validation;

public sealed class IdentityNumberNormalizerTests
{
    [Fact]
    public void NormalizeTurkishIdentityNumber_PreservesValidElevenDigits()
    {
        Assert.Equal("10000000146", IdentityNumberNormalizer.NormalizeTurkishIdentityNumber("10000000146"));
        Assert.Equal("11111111110", IdentityNumberNormalizer.NormalizeTurkishIdentityNumber(" 11111111110 "));
    }

    [Fact]
    public void NormalizeForeignIdentityNumber_RemovesSpacesAndHyphens()
    {
        Assert.Equal("99000000001", IdentityNumberNormalizer.NormalizeForeignIdentityNumber("990-000-000-01"));
        Assert.Equal("99000000001", IdentityNumberNormalizer.NormalizeForeignIdentityNumber("990 000 000 01"));
    }

    [Fact]
    public void NormalizePassportNumber_UppercasesAndRemovesSeparators()
    {
        Assert.Equal("AB1234567", IdentityNumberNormalizer.NormalizePassportNumber("ab 123-4567"));
        Assert.Equal("P12345", IdentityNumberNormalizer.NormalizePassportNumber("p12345"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_ReturnsNullForEmptyInput(string? value)
    {
        Assert.Null(IdentityNumberNormalizer.NormalizeTurkishIdentityNumber(value));
        Assert.Null(IdentityNumberNormalizer.NormalizeForeignIdentityNumber(value));
        Assert.Null(IdentityNumberNormalizer.NormalizePassportNumber(value));
        Assert.Null(IdentityNumberNormalizer.Normalize(IdentityDocumentType.Passport, value));
    }

    [Theory]
    [InlineData("AB12İ34")]
    [InlineData("AB#1234")]
    [InlineData("AB 中文")]
    public void NormalizePassportNumber_RejectsUnicodeAndSpecialCharacters(string value)
    {
        Assert.Null(IdentityNumberNormalizer.NormalizePassportNumber(value));
    }

    [Fact]
    public void NormalizeTurkishIdentityNumber_RejectsNonDigitCharacters()
    {
        Assert.Null(IdentityNumberNormalizer.NormalizeTurkishIdentityNumber("1000000014A"));
        Assert.Null(IdentityNumberNormalizer.NormalizeTurkishIdentityNumber("1000000014"));
    }

    [Fact]
    public void Normalize_DispatchesByDocumentType()
    {
        Assert.Equal("10000000146",
            IdentityNumberNormalizer.Normalize(IdentityDocumentType.TurkishIdentityNumber, "10000000146"));
        Assert.Equal("99000000001",
            IdentityNumberNormalizer.Normalize(IdentityDocumentType.ForeignIdentityNumber, "990-000-000-01"));
        Assert.Equal("AB12345",
            IdentityNumberNormalizer.Normalize(IdentityDocumentType.Passport, "ab12345"));
    }
}
