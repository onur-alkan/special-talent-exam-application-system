using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.Services;
using OzelYetenekSinavSistemi.Domain.Enums;

namespace OzelYetenekSinavSistemi.Tests.Validation;

public sealed class IdentityDocumentValidatorTests
{
    private static readonly DateOnly Today = new(2026, 3, 20);
    private readonly IdentityDocumentValidator _sut = new(new FixedUtcTimeProvider(Today));

    [Fact]
    public void Validate_TurkishIdentityNumber_AcceptsValidTc()
    {
        var result = _sut.Validate(new IdentityDocumentValidationRequest
        {
            DocumentType = IdentityDocumentType.TurkishIdentityNumber,
            IdentityNumber = "10000000146",
            NationalityCountryCode = "TR"
        });

        Assert.True(result.IsValid);
        Assert.Equal("10000000146", result.NormalizedIdentityNumber);
        Assert.Equal("TR", result.NormalizedNationalityCountryCode);
        Assert.Null(result.NormalizedIssuingCountryCode);
    }

    [Fact]
    public void Validate_TurkishIdentityNumber_RejectsInvalidChecksum()
    {
        var result = _sut.Validate(new IdentityDocumentValidationRequest
        {
            DocumentType = IdentityDocumentType.TurkishIdentityNumber,
            IdentityNumber = "12345678901",
            NationalityCountryCode = "TR"
        });

        Assert.False(result.IsValid);
        Assert.Contains("T.C.", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_TurkishIdentityNumber_RejectsPassportFields()
    {
        var result = _sut.Validate(new IdentityDocumentValidationRequest
        {
            DocumentType = IdentityDocumentType.TurkishIdentityNumber,
            IdentityNumber = "10000000146",
            NationalityCountryCode = "TR",
            IssuingCountryCode = "DE",
            PassportExpiryDate = Today.AddDays(1)
        });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_ForeignIdentityNumber_DoesNotApplyTurkishChecksum()
    {
        var result = _sut.Validate(new IdentityDocumentValidationRequest
        {
            DocumentType = IdentityDocumentType.ForeignIdentityNumber,
            IdentityNumber = "99999999999",
            NationalityCountryCode = "DE"
        });

        Assert.True(result.IsValid);
        Assert.Equal("99999999999", result.NormalizedIdentityNumber);
    }

    [Fact]
    public void Validate_ForeignIdentityNumber_RejectsWhenNotStartingWithNine()
    {
        var result = _sut.Validate(new IdentityDocumentValidationRequest
        {
            DocumentType = IdentityDocumentType.ForeignIdentityNumber,
            IdentityNumber = "89000000001",
            NationalityCountryCode = "DE"
        });

        Assert.False(result.IsValid);
        Assert.Contains("9 ile başlamalı", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ForeignIdentityNumber_RejectsWhenNotElevenDigits()
    {
        var result = _sut.Validate(new IdentityDocumentValidationRequest
        {
            DocumentType = IdentityDocumentType.ForeignIdentityNumber,
            IdentityNumber = "9900000001",
            NationalityCountryCode = "DE"
        });

        Assert.False(result.IsValid);
        Assert.Contains("11 haneli", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ForeignIdentityNumber_RejectsTurkishNationality()
    {
        var result = _sut.Validate(new IdentityDocumentValidationRequest
        {
            DocumentType = IdentityDocumentType.ForeignIdentityNumber,
            IdentityNumber = "99000000001",
            NationalityCountryCode = "TR"
        });

        Assert.False(result.IsValid);
        Assert.Contains("TR olamaz", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_Passport_AcceptsValidRequest()
    {
        var result = _sut.Validate(new IdentityDocumentValidationRequest
        {
            DocumentType = IdentityDocumentType.Passport,
            IdentityNumber = "ab12345",
            NationalityCountryCode = "de",
            IssuingCountryCode = "us",
            PassportExpiryDate = Today.AddDays(30)
        });

        Assert.True(result.IsValid);
        Assert.Equal("AB12345", result.NormalizedIdentityNumber);
        Assert.Equal("DE", result.NormalizedNationalityCountryCode);
        Assert.Equal("US", result.NormalizedIssuingCountryCode);
    }

    [Fact]
    public void Validate_Passport_RejectsMissingIssuingCountry()
    {
        var result = _sut.Validate(new IdentityDocumentValidationRequest
        {
            DocumentType = IdentityDocumentType.Passport,
            IdentityNumber = "AB12345",
            NationalityCountryCode = "DE",
            PassportExpiryDate = Today.AddDays(30)
        });

        Assert.False(result.IsValid);
        Assert.Contains("düzenleyen ülke", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Passport_RejectsMissingExpiryDate()
    {
        var result = _sut.Validate(new IdentityDocumentValidationRequest
        {
            DocumentType = IdentityDocumentType.Passport,
            IdentityNumber = "AB12345",
            NationalityCountryCode = "DE",
            IssuingCountryCode = "US",
            PassportExpiryDate = null
        });

        Assert.False(result.IsValid);
        Assert.Contains("geçerlilik tarihi", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Passport_RejectsExpiredDate()
    {
        var result = _sut.Validate(new IdentityDocumentValidationRequest
        {
            DocumentType = IdentityDocumentType.Passport,
            IdentityNumber = "AB12345",
            NationalityCountryCode = "DE",
            IssuingCountryCode = "US",
            PassportExpiryDate = Today.AddDays(-1)
        });

        Assert.False(result.IsValid);
        Assert.Contains("geçmiş olamaz", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("T")]
    [InlineData("TUR")]
    [InlineData("1R")]
    public void Validate_RejectsInvalidCountryCodeFormat(string countryCode)
    {
        var result = _sut.Validate(new IdentityDocumentValidationRequest
        {
            DocumentType = IdentityDocumentType.Passport,
            IdentityNumber = "AB12345",
            NationalityCountryCode = countryCode,
            IssuingCountryCode = "US",
            PassportExpiryDate = Today.AddDays(10)
        });

        Assert.False(result.IsValid);
        Assert.Contains("uyruk", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FixedUtcTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedUtcTimeProvider(DateOnly today)
            => _utcNow = new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
