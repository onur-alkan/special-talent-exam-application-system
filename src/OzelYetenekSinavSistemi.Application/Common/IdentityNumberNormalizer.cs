using OzelYetenekSinavSistemi.Domain.Common;
using OzelYetenekSinavSistemi.Domain.Enums;

namespace OzelYetenekSinavSistemi.Application.Common;

/// <summary>
/// Kimlik belge numaralarını türüne göre normalize eder. Ham değer loglanmaz.
/// </summary>
public static class IdentityNumberNormalizer
{
    public static string? Normalize(IdentityDocumentType documentType, string? value) =>
        documentType switch
        {
            IdentityDocumentType.TurkishIdentityNumber => NormalizeTurkishIdentityNumber(value),
            IdentityDocumentType.ForeignIdentityNumber => NormalizeForeignIdentityNumber(value),
            IdentityDocumentType.Passport => NormalizePassportNumber(value),
            _ => null
        };

    public static string? NormalizeTurkishIdentityNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return ExtractDigits(value.Trim(), DomainConstants.TcNoLength);
    }

    public static string? NormalizeForeignIdentityNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var stripped = RemoveSpacesAndHyphens(value.Trim());
        return ExtractDigitsOnly(stripped);
    }

    public static string? NormalizePassportNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var stripped = RemoveSpacesAndHyphens(value.Trim()).ToUpperInvariant();
        if (stripped.Length == 0)
            return null;

        foreach (var ch in stripped)
        {
            var isDigit = ch is >= '0' and <= '9';
            var isUpperAscii = ch is >= 'A' and <= 'Z';
            if (!isDigit && !isUpperAscii)
                return null;
        }

        return stripped;
    }

    private static string? ExtractDigits(string value, int? exactLength)
    {
        var normalized = ExtractDigitsOnly(value);
        if (normalized is null)
            return null;

        if (exactLength is not null && normalized.Length != exactLength.Value)
            return null;

        return normalized;
    }

    private static string? ExtractDigitsOnly(string value)
    {
        if (value.Length == 0)
            return null;

        Span<char> buffer = stackalloc char[value.Length];
        var digitCount = 0;

        foreach (var ch in value)
        {
            if (ch is >= '0' and <= '9')
                buffer[digitCount++] = ch;
            else
                return null;
        }

        return digitCount == 0 ? null : new string(buffer[..digitCount]);
    }

    private static string RemoveSpacesAndHyphens(string value)
    {
        Span<char> buffer = stackalloc char[value.Length];
        var length = 0;

        foreach (var ch in value)
        {
            if (ch is ' ' or '-')
                continue;

            buffer[length++] = ch;
        }

        return length == 0 ? string.Empty : new string(buffer[..length]);
    }
}
