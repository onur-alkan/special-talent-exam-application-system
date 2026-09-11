using System.Globalization;

namespace OzelYetenekSinavSistemi.Application.Validation;

/// <summary>
/// Bilinen sistem ayarları için anahtar-bazlı doğrulama.
/// </summary>
public static class SystemSettingValidator
{
    public const string MaxPhotoSizeKbKey = "MaxPhotoSizeKb";

    private const int KeyMaxLength = 100;
    private const int MinPhotoSizeKb = 100;
    private const int MaxPhotoSizeKb = 10240;

    private static readonly HashSet<string> KnownKeys =
        new(StringComparer.OrdinalIgnoreCase) { MaxPhotoSizeKbKey };

    public static bool IsKnownKey(string? key) =>
        !string.IsNullOrWhiteSpace(key) && KnownKeys.Contains(key.Trim());

    public static bool TryValidateKey(string? key, out string? normalizedKey, out string? errorMessage)
    {
        normalizedKey = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(key))
        {
            errorMessage = "Ayar anahtarını giriniz.";
            return false;
        }

        normalizedKey = key.Trim();
        if (normalizedKey.Length > KeyMaxLength)
        {
            errorMessage = "Ayar anahtarı çok uzun.";
            return false;
        }

        if (InputTextRules.ContainsForbiddenCharacters(normalizedKey))
        {
            errorMessage = "Ayar anahtarındaki uygun olmayan karakterleri kaldırınız.";
            return false;
        }

        if (!KnownKeys.Contains(normalizedKey))
        {
            errorMessage = "Bilinmeyen sistem ayarı.";
            return false;
        }

        return true;
    }

    public static bool TryValidateValue(string normalizedKey, string? value, out string? normalizedValue, out string? errorMessage)
    {
        normalizedValue = null;
        errorMessage = null;

        if (string.Equals(normalizedKey, MaxPhotoSizeKbKey, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                errorMessage = "Fotoğraf boyutu değerini giriniz.";
                return false;
            }

            var trimmed = value.Trim();
            if (!int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var kb)
                || kb < MinPhotoSizeKb
                || kb > MaxPhotoSizeKb)
            {
                errorMessage = $"Fotoğraf boyutu {MinPhotoSizeKb}-{MaxPhotoSizeKb} KB aralığında olmalıdır.";
                return false;
            }

            normalizedValue = kb.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        errorMessage = "Bilinmeyen sistem ayarı.";
        return false;
    }
}
