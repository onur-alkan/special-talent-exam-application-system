using OzelYetenekSinavSistemi.Domain.Enums;

namespace OzelYetenekSinavSistemi.Application.Common;

/// <summary>
/// Kimlik belgesi türüne göre kullanıcıya gösterilen zorunluluk mesajları.
/// </summary>
public static class IdentityDocumentMessages
{
    public const string RequiredTurkish = "T.C. Kimlik Numaranızı giriniz.";
    public const string RequiredForeign = "Yabancı kimlik numaranızı giriniz.";
    public const string RequiredPassport = "Pasaport numaranızı giriniz.";

    /// <summary>Genel/eski mesaj; kullanılmamalı.</summary>
    public const string RequiredGenericLegacy = "Kimlik numaranızı giriniz.";

    public static string RequiredNumber(IdentityDocumentType? documentType) => documentType switch
    {
        IdentityDocumentType.ForeignIdentityNumber => RequiredForeign,
        IdentityDocumentType.Passport => RequiredPassport,
        _ => RequiredTurkish
    };
}
