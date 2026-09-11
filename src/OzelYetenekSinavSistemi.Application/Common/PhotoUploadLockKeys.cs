using System.Security.Cryptography;
using System.Text;
using OzelYetenekSinavSistemi.Domain.Enums;

namespace OzelYetenekSinavSistemi.Application.Common;

/// <summary>
/// Fotoğraf yükleme kritik bölümleri için keyed lock anahtarları.
/// Ham kimlik veya e-posta loglanmaz; anahtarlar bellek içi mutex için türetilir.
/// </summary>
public static class PhotoUploadLockKeys
{
    public static string ForRegister(
        IdentityDocumentType documentType,
        string normalizedIdentityNumber,
        string? issuingCountryCode,
        string email)
    {
        var normalizedIdentity = normalizedIdentityNumber.Trim();
        var normalizedEmail = email.Trim().ToUpperInvariant();
        var normalizedIssuing = (issuingCountryCode ?? string.Empty).Trim().ToUpperInvariant();
        var material = ((byte)documentType).ToString()
            + "\0" + normalizedIdentity
            + "\0" + normalizedIssuing
            + "\0" + normalizedEmail;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
        return "photo-upload:register:" + hash;
    }

    public static string ForRegister(string tcNo, string email) =>
        ForRegister(IdentityDocumentType.TurkishIdentityNumber, tcNo.Trim(), null, email);

    public static string ForProfile(Guid userId) => "photo-upload:profile:" + userId.ToString("D");
}
