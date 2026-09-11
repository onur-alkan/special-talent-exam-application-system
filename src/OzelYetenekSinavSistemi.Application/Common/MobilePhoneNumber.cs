using System.Text;
using PhoneNumbers;

namespace OzelYetenekSinavSistemi.Application.Common;

/// <summary>
/// Cep telefonu girdisini libphonenumber ile doğrular ve E.164 biçimine çevirir.
/// </summary>
public static class MobilePhoneNumber
{
    public const string DefaultRegionCode = "TR";
    public const int MaxInputLength = 32;
    public const int MaxE164Length = 16; // '+' + en fazla 15 rakam

    public const string RequiredMessage = "Cep telefonu numaranızı giriniz.";
    public const string InvalidMessage = "Geçerli bir cep telefonu numarası giriniz.";
    public const string CountryMismatchMessage = "Seçtiğiniz ülkeye uygun geçerli bir cep telefonu numarası giriniz.";
    public const string UnexpectedMessage = "Telefon numarası doğrulanamadı. Lütfen numaranızı kontrol ederek yeniden deneyiniz.";
    public const string CountryRequiredMessage = "Telefon için ülke seçiniz.";

    public static bool TryValidateAndNormalize(
        string? regionCode,
        string? rawInput,
        out string? e164,
        out string? errorMessage)
    {
        e164 = null;
        errorMessage = null;

        try
        {
            if (string.IsNullOrWhiteSpace(rawInput))
            {
                errorMessage = RequiredMessage;
                return false;
            }

            var input = rawInput.Trim();
            if (input.Length > MaxInputLength)
            {
                errorMessage = InvalidMessage;
                return false;
            }

            if (!IsSafePhoneInput(input))
            {
                errorMessage = InvalidMessage;
                return false;
            }

            var region = NormalizeRegionCode(regionCode);
            if (region is null)
            {
                errorMessage = CountryRequiredMessage;
                return false;
            }

            var util = PhoneNumberUtil.GetInstance();
            if (util.GetCountryCodeForRegion(region) == 0)
            {
                errorMessage = CountryMismatchMessage;
                return false;
            }

            if (!TryParse(util, input, region, out var number, out errorMessage))
                return false;

            if (!util.IsValidNumber(number))
            {
                errorMessage = InvalidMessage;
                return false;
            }

            var actualRegion = util.GetRegionCodeForNumber(number);
            if (string.IsNullOrEmpty(actualRegion)
                || string.Equals(actualRegion, "ZZ", StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = InvalidMessage;
                return false;
            }

            if (!string.Equals(actualRegion, region, StringComparison.OrdinalIgnoreCase)
                || !util.IsValidNumberForRegion(number, region))
            {
                errorMessage = CountryMismatchMessage;
                return false;
            }

            if (!IsAcceptableMobileType(util.GetNumberType(number)))
            {
                errorMessage = InvalidMessage;
                return false;
            }

            var formatted = util.Format(number, PhoneNumberFormat.E164);
            if (string.IsNullOrEmpty(formatted)
                || formatted.Length > MaxE164Length
                || formatted[0] != '+'
                || formatted.Length < 3)
            {
                errorMessage = UnexpectedMessage;
                return false;
            }

            e164 = formatted;
            return true;
        }
        catch
        {
            errorMessage = UnexpectedMessage;
            e164 = null;
            return false;
        }
    }

    public static string Mask(string? e164OrRaw)
    {
        if (string.IsNullOrWhiteSpace(e164OrRaw))
            return string.Empty;

        var value = e164OrRaw.Trim();
        if (value.Length <= 6)
            return new string('*', value.Length);

        var prefixLength = Math.Min(3, value.Length - 4);
        var suffixLength = Math.Min(4, value.Length - prefixLength);
        var maskLength = value.Length - prefixLength - suffixLength;
        return string.Concat(
            value.AsSpan(0, prefixLength),
            new string('*', Math.Max(maskLength, 0)),
            value.AsSpan(value.Length - suffixLength));
    }

    public static bool TryFormatForDisplay(string? stored, out string display)
    {
        display = stored?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(display))
            return false;

        try
        {
            var util = PhoneNumberUtil.GetInstance();
            PhoneNumber number;
            if (display.StartsWith('+'))
                number = util.Parse(display, null);
            else if (IsLegacyTurkishDigits(display))
                number = util.Parse(display, DefaultRegionCode);
            else
                return false;

            if (!util.IsValidNumber(number))
                return false;

            var region = util.GetRegionCodeForNumber(number);
            if (string.Equals(region, DefaultRegionCode, StringComparison.OrdinalIgnoreCase))
            {
                var national = util.GetNationalSignificantNumber(number);
                display = FormatTurkishNational(national);
                return true;
            }

            display = util.Format(number, PhoneNumberFormat.INTERNATIONAL);
            return true;
        }
        catch
        {
            display = stored!.Trim();
            return false;
        }
    }

    public static string? NormalizeRegionCode(string? regionCode)
    {
        if (string.IsNullOrWhiteSpace(regionCode))
            return null;

        var normalized = regionCode.Trim().ToUpperInvariant();
        if (normalized.Length != 2)
            return null;

        foreach (var ch in normalized)
        {
            if (ch is < 'A' or > 'Z')
                return null;
        }

        return normalized;
    }

    public static bool IsSafePhoneInput(string input)
    {
        foreach (var ch in input)
        {
            if (char.IsDigit(ch))
                continue;
            if (ch is '+' or '-' or '(' or ')' or ' ' or '.')
                continue;
            // Narrow no-break space / regular separators occasionally pasted
            if (ch is '\u00A0' or '\u202F')
                continue;
            return false;
        }

        return true;
    }

    private static bool TryParse(
        PhoneNumberUtil util,
        string input,
        string region,
        out PhoneNumber number,
        out string? errorMessage)
    {
        number = null!;
        errorMessage = null;

        try
        {
            if (input.Contains('+'))
            {
                number = util.Parse(input, DefaultRegionCode);
                return true;
            }

            number = util.Parse(input, region);
            return true;
        }
        catch (NumberParseException)
        {
            errorMessage = InvalidMessage;
            return false;
        }
    }

    private static bool IsAcceptableMobileType(PhoneNumberType type)
        => type is PhoneNumberType.MOBILE or PhoneNumberType.FIXED_LINE_OR_MOBILE;

    private static bool IsLegacyTurkishDigits(string value)
    {
        var digits = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsDigit(ch))
                digits.Append(ch);
        }

        var text = digits.ToString();
        return text.Length is 10 or 11
               && (text.StartsWith('5') || text.StartsWith("05", StringComparison.Ordinal));
    }

    private static string FormatTurkishNational(string nationalDigits)
    {
        var digits = new string(nationalDigits.Where(char.IsDigit).ToArray());
        if (digits.Length != 10)
            return digits;

        return $"{digits[..3]} {digits[3..6]} {digits[6..8]} {digits[8..10]}";
    }
}
