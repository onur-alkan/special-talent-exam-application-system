using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Domain.Common;
using OzelYetenekSinavSistemi.Domain.Enums;

namespace OzelYetenekSinavSistemi.Application.Services;

public sealed class IdentityDocumentValidator : IIdentityDocumentValidator
{
    private const string TurkeyCountryCode = "TR";

    private readonly TimeProvider _timeProvider;

    public IdentityDocumentValidator(TimeProvider timeProvider) => _timeProvider = timeProvider;

    public IdentityDocumentValidationResult Validate(IdentityDocumentValidationRequest request)
    {
        return request.DocumentType switch
        {
            IdentityDocumentType.TurkishIdentityNumber => ValidateTurkish(request),
            IdentityDocumentType.ForeignIdentityNumber => ValidateForeign(request),
            IdentityDocumentType.Passport => ValidatePassport(request),
            _ => IdentityDocumentValidationResult.Fail("Geçersiz kimlik belgesi türü.")
        };
    }

    private IdentityDocumentValidationResult ValidateTurkish(IdentityDocumentValidationRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.IssuingCountryCode))
            return IdentityDocumentValidationResult.Fail("T.C. Kimlik Numarası için pasaport veren ülke belirtilmemelidir.");

        if (request.PassportExpiryDate is not null)
            return IdentityDocumentValidationResult.Fail("T.C. Kimlik Numarası için pasaport geçerlilik tarihi belirtilmemelidir.");

        if (string.IsNullOrWhiteSpace(request.IdentityNumber))
            return IdentityDocumentValidationResult.Fail(IdentityDocumentMessages.RequiredTurkish);

        var normalizedIdentity = IdentityNumberNormalizer.NormalizeTurkishIdentityNumber(request.IdentityNumber);
        if (normalizedIdentity is null)
            return IdentityDocumentValidationResult.Fail("Geçerli bir T.C. Kimlik Numarası giriniz.");

        if (!TurkishIdentityNumber.IsValid(normalizedIdentity))
            return IdentityDocumentValidationResult.Fail(TurkishIdentityNumber.InvalidMessage);

        var nationality = NormalizeCountryCode(request.NationalityCountryCode);
        if (nationality is null)
            return IdentityDocumentValidationResult.Fail("Geçerli bir uyruk seçiniz.");

        if (!string.Equals(nationality, TurkeyCountryCode, StringComparison.Ordinal))
            return IdentityDocumentValidationResult.Fail("T.C. Kimlik Numarası için uyruk TR olmalıdır.");

        return IdentityDocumentValidationResult.Ok(normalizedIdentity, nationality, null);
    }

    private IdentityDocumentValidationResult ValidateForeign(IdentityDocumentValidationRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.IssuingCountryCode))
            return IdentityDocumentValidationResult.Fail("Yabancı Kimlik Numarası için pasaport veren ülke belirtilmemelidir.");

        if (request.PassportExpiryDate is not null)
            return IdentityDocumentValidationResult.Fail("Yabancı Kimlik Numarası için pasaport geçerlilik tarihi belirtilmemelidir.");

        if (string.IsNullOrWhiteSpace(request.IdentityNumber))
            return IdentityDocumentValidationResult.Fail(IdentityDocumentMessages.RequiredForeign);

        var normalizedIdentity = IdentityNumberNormalizer.NormalizeForeignIdentityNumber(request.IdentityNumber);
        if (normalizedIdentity is null)
            return IdentityDocumentValidationResult.Fail("Geçerli bir Yabancı Kimlik Numarası giriniz.");

        if (normalizedIdentity.Length != DomainConstants.TcNoLength)
            return IdentityDocumentValidationResult.Fail("Yabancı Kimlik Numarası 11 haneli olmalıdır.");

        if (normalizedIdentity[0] != '9')
            return IdentityDocumentValidationResult.Fail("Yabancı Kimlik Numarası 9 ile başlamalıdır.");

        var nationality = NormalizeCountryCode(request.NationalityCountryCode);
        if (nationality is null)
            return IdentityDocumentValidationResult.Fail("Geçerli bir uyruk seçiniz.");

        if (string.Equals(nationality, TurkeyCountryCode, StringComparison.Ordinal))
            return IdentityDocumentValidationResult.Fail("Yabancı Kimlik Numarası için uyruk TR olamaz.");

        return IdentityDocumentValidationResult.Ok(normalizedIdentity, nationality, null);
    }

    private IdentityDocumentValidationResult ValidatePassport(IdentityDocumentValidationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.IdentityNumber))
            return IdentityDocumentValidationResult.Fail(IdentityDocumentMessages.RequiredPassport);

        var normalizedIdentity = IdentityNumberNormalizer.NormalizePassportNumber(request.IdentityNumber);
        if (normalizedIdentity is null)
            return IdentityDocumentValidationResult.Fail("Geçerli bir pasaport numarası giriniz.");

        if (normalizedIdentity.Length < DomainConstants.PassportNumberMinLength
            || normalizedIdentity.Length > DomainConstants.PassportNumberMaxLength)
        {
            return IdentityDocumentValidationResult.Fail(
                $"Pasaport numarası {DomainConstants.PassportNumberMinLength}-{DomainConstants.PassportNumberMaxLength} karakter olmalıdır.");
        }

        var nationality = NormalizeCountryCode(request.NationalityCountryCode);
        if (nationality is null)
            return IdentityDocumentValidationResult.Fail("Geçerli bir uyruk seçiniz.");

        if (string.Equals(nationality, TurkeyCountryCode, StringComparison.Ordinal))
            return IdentityDocumentValidationResult.Fail("Pasaport için uyruk TR olamaz.");

        var issuingCountry = NormalizeCountryCode(request.IssuingCountryCode);
        if (issuingCountry is null)
            return IdentityDocumentValidationResult.Fail("Pasaportu düzenleyen ülkeyi seçiniz.");

        if (request.PassportExpiryDate is null)
            return IdentityDocumentValidationResult.Fail("Pasaport geçerlilik tarihini giriniz.");

        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        if (request.PassportExpiryDate.Value < today)
            return IdentityDocumentValidationResult.Fail("Pasaport geçerlilik tarihi geçmiş olamaz.");

        return IdentityDocumentValidationResult.Ok(normalizedIdentity, nationality, issuingCountry);
    }

    private static string? NormalizeCountryCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        if (trimmed.Length != DomainConstants.CountryCodeLength)
            return null;

        var upper = trimmed.ToUpperInvariant();
        foreach (var ch in upper)
        {
            if (ch is < 'A' or > 'Z')
                return null;
        }

        return upper;
    }
}
