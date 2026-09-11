using System.Globalization;
using OzelYetenekSinavSistemi.Application.DTOs.Applications;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Domain.Entities;
using OzelYetenekSinavSistemi.Domain.Enums;

namespace OzelYetenekSinavSistemi.Application.Common;

public static class IdentityDisplayBuilder
{
    private const string TurkeyDisplayName = "Türkiye";
    private const string EmptyDisplay = "-";

    public static IdentityDisplayInfo BuildFromUser(
        User user,
        ICountryCatalog countryCatalog,
        bool maskIdentity,
        ISensitiveDataMaskingService? maskingService = null) =>
        Build(
            user.IdentityDocumentType,
            user.IdentityNumber,
            user.TcNo,
            user.NationalityCountryCode,
            user.IssuingCountryCode,
            user.PassportExpiryDate,
            user.Nationality,
            countryCatalog,
            maskIdentity,
            maskingService);

    public static IdentityDisplayInfo BuildFromApplicationDetail(
        CandidateApplicationDetail detail,
        ICountryCatalog countryCatalog,
        bool maskIdentity,
        ISensitiveDataMaskingService? maskingService = null) =>
        Build(
            detail.IdentityDocumentType,
            detail.IdentityNumber,
            detail.TcNo,
            detail.NationalityCountryCode,
            detail.IssuingCountryCode,
            detail.PassportExpiryDate,
            legacyNationality: null,
            countryCatalog,
            maskIdentity,
            maskingService);

    private static IdentityDisplayInfo Build(
        IdentityDocumentType? documentType,
        string? identityNumber,
        string? legacyTcNo,
        string? nationalityCountryCode,
        string? issuingCountryCode,
        DateOnly? passportExpiryDate,
        string? legacyNationality,
        ICountryCatalog countryCatalog,
        bool maskIdentity,
        ISensitiveDataMaskingService? maskingService)
    {
        var resolvedType = ResolveDocumentType(documentType, legacyTcNo, identityNumber);
        var rawNumber = ResolveRawIdentityNumber(resolvedType, identityNumber, legacyTcNo);

        var displayNumber = maskIdentity && maskingService is not null
            ? maskingService.MaskIdentityNumber(resolvedType, rawNumber, legacyTcNo)
            : rawNumber;

        if (string.IsNullOrWhiteSpace(displayNumber))
            displayNumber = EmptyDisplay;

        return resolvedType switch
        {
            IdentityDocumentType.TurkishIdentityNumber => new IdentityDisplayInfo
            {
                IdentityDocumentTypeDisplayName = "T.C. Kimlik Kartı",
                IdentityNumberLabel = "T.C. Kimlik Numarası",
                DisplayIdentityNumber = displayNumber,
                NationalityDisplayName = TurkeyDisplayName
            },
            IdentityDocumentType.ForeignIdentityNumber => new IdentityDisplayInfo
            {
                IdentityDocumentTypeDisplayName = "Yabancı Kimlik Numarası",
                IdentityNumberLabel = "Yabancı Kimlik Numarası",
                DisplayIdentityNumber = displayNumber,
                NationalityDisplayName = ResolveCountryDisplay(nationalityCountryCode, countryCatalog, legacyNationality)
            },
            IdentityDocumentType.Passport => new IdentityDisplayInfo
            {
                IdentityDocumentTypeDisplayName = "Pasaport",
                IdentityNumberLabel = "Pasaport Numarası",
                DisplayIdentityNumber = displayNumber,
                NationalityDisplayName = ResolveCountryDisplay(nationalityCountryCode, countryCatalog, legacyNationality),
                IssuingCountryDisplayName = ResolveCountryDisplay(issuingCountryCode, countryCatalog, null),
                PassportExpiryDateDisplay = FormatPassportExpiry(passportExpiryDate)
            },
            _ when !string.IsNullOrWhiteSpace(legacyTcNo) => Build(
                IdentityDocumentType.TurkishIdentityNumber,
                identityNumber,
                legacyTcNo,
                nationalityCountryCode,
                issuingCountryCode,
                passportExpiryDate,
                legacyNationality,
                countryCatalog,
                maskIdentity,
                maskingService),
            _ => new IdentityDisplayInfo
            {
                IdentityDocumentTypeDisplayName = EmptyDisplay,
                IdentityNumberLabel = "Kimlik Numarası",
                DisplayIdentityNumber = string.IsNullOrWhiteSpace(rawNumber) ? EmptyDisplay : displayNumber,
                NationalityDisplayName = ResolveCountryDisplay(nationalityCountryCode, countryCatalog, legacyNationality)
            }
        };
    }

    private static IdentityDocumentType? ResolveDocumentType(
        IdentityDocumentType? documentType,
        string? legacyTcNo,
        string? identityNumber)
    {
        if (documentType.HasValue)
            return documentType.Value;

        if (!string.IsNullOrWhiteSpace(legacyTcNo))
            return IdentityDocumentType.TurkishIdentityNumber;

        return null;
    }

    private static string? ResolveRawIdentityNumber(
        IdentityDocumentType? documentType,
        string? identityNumber,
        string? legacyTcNo)
    {
        if (!string.IsNullOrWhiteSpace(identityNumber))
            return identityNumber.Trim();

        if (documentType is null or IdentityDocumentType.TurkishIdentityNumber
            && !string.IsNullOrWhiteSpace(legacyTcNo))
            return legacyTcNo.Trim();

        return null;
    }

    private static string ResolveCountryDisplay(
        string? countryCode,
        ICountryCatalog countryCatalog,
        string? legacyNationality)
    {
        if (countryCatalog.TryGetByCode(countryCode, out var country))
            return country!.DisplayName;

        if (string.Equals(countryCode, "TR", StringComparison.OrdinalIgnoreCase))
            return TurkeyDisplayName;

        if (!string.IsNullOrWhiteSpace(legacyNationality))
            return legacyNationality.Trim();

        return EmptyDisplay;
    }

    private static string FormatPassportExpiry(DateOnly? expiry)
    {
        if (!expiry.HasValue)
            return EmptyDisplay;

        return expiry.Value.ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("tr-TR"));
    }
}
