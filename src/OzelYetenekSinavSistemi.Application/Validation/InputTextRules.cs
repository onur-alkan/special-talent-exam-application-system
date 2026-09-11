using System.Globalization;
using System.Text;

namespace OzelYetenekSinavSistemi.Application.Validation;

/// <summary>
/// Unicode farkındalıklı metin doğrulama ve tek satırlı normalizasyon kuralları.
/// Parola, token ve hash alanlarına uygulanmamalıdır.
/// </summary>
public static class InputTextRules
{
    public const string HumanNameInvalidMessageTemplate =
        "{0}ınızı harf kullanarak giriniz.";

    public const string MeaningfulTitleInvalidMessageTemplate =
        "{0} için anlamlı bir başlık giriniz.";

    public const string MeaningfulTextInvalidMessageTemplate =
        "{0} için anlamlı bir metin giriniz.";

    private static readonly char[] ZeroWidthCharacters =
    [
        '\u200B', '\u200C', '\u200D', '\uFEFF', '\u2060', '\u00AD'
    ];

    private static readonly char[] BidiAndFormatCharacters =
    [
        '\u200E', '\u200F', '\u202A', '\u202B', '\u202C', '\u202D', '\u202E',
        '\u2066', '\u2067', '\u2068', '\u2069'
    ];

    public static string FormatHumanNameMessage(string fieldLabel) =>
        string.Format(CultureInfo.CurrentCulture, HumanNameInvalidMessageTemplate, fieldLabel);

    public static string FormatMeaningfulTitleMessage(string fieldLabel) =>
        string.Format(CultureInfo.CurrentCulture, MeaningfulTitleInvalidMessageTemplate, fieldLabel);

    public static string FormatMeaningfulTextMessage(string fieldLabel) =>
        string.Format(CultureInfo.CurrentCulture, MeaningfulTextInvalidMessageTemplate, fieldLabel);

    public static bool ContainsForbiddenCharacters(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        foreach (var ch in value)
        {
            if (char.IsControl(ch) && ch is not '\r' and not '\n' and not '\t')
                return true;

            if (ZeroWidthCharacters.Contains(ch) || BidiAndFormatCharacters.Contains(ch))
                return true;
        }

        return false;
    }

    public static string? NormalizeSingleLineWhitespace(string? value)
    {
        if (value is null)
            return null;

        var trimmed = value.Trim();
        if (trimmed.Length == 0)
            return string.Empty;

        var builder = new StringBuilder(trimmed.Length);
        var previousWasSpace = false;

        foreach (var raw in trimmed)
        {
            var ch = raw is '\r' or '\n' or '\t' ? ' ' : raw;

            if (char.IsWhiteSpace(ch))
            {
                if (!previousWasSpace)
                {
                    builder.Append(' ');
                    previousWasSpace = true;
                }

                continue;
            }

            previousWasSpace = false;
            builder.Append(ch);
        }

        return builder.ToString();
    }

    public static string? NormalizeHumanName(string? value)
    {
        var normalized = NormalizeSingleLineWhitespace(value);
        return string.IsNullOrEmpty(normalized) ? normalized : normalized;
    }

    public static string? NormalizeMultilineText(string? value)
    {
        if (value is null)
            return null;

        var lines = value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length == 0)
            return string.Empty;

        return string.Join(Environment.NewLine, lines.Select(line => NormalizeSingleLineWhitespace(line) ?? string.Empty));
    }

    public static bool IsValidHumanName(string? value, out string? normalized)
    {
        normalized = NormalizeHumanName(value);
        if (string.IsNullOrEmpty(normalized))
            return false;

        if (ContainsForbiddenCharacters(normalized))
            return false;

        if (!ContainsLetter(normalized))
            return false;

        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0)
            return false;

        foreach (var word in words)
        {
            if (!IsValidHumanNameWord(word))
                return false;
        }

        return true;
    }

    public static bool IsValidMeaningfulTitle(string? value, out string? normalized)
    {
        normalized = NormalizeSingleLineWhitespace(value);
        if (string.IsNullOrEmpty(normalized))
            return false;

        if (ContainsForbiddenCharacters(normalized))
            return false;

        if (!ContainsLetter(normalized))
            return false;

        return HasMeaningfulContent(normalized);
    }

    public static bool IsValidRequiredMeaningfulText(string? value, bool multiline, out string? normalized)
    {
        normalized = multiline ? NormalizeMultilineText(value) : NormalizeSingleLineWhitespace(value);
        if (string.IsNullOrEmpty(normalized))
            return false;

        if (ContainsForbiddenCharacters(normalized))
            return false;

        return HasMeaningfulContent(normalized);
    }

    public static bool IsValidOptionalMeaningfulText(string? value, bool multiline, out string? normalized)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            normalized = null;
            return true;
        }

        return IsValidRequiredMeaningfulText(value, multiline, out normalized);
    }

    private static bool HasMeaningfulContent(string value)
    {
        var hasLetter = false;
        var hasDigit = false;

        foreach (var ch in value)
        {
            if (char.IsLetter(ch))
                hasLetter = true;
            else if (char.IsDigit(ch))
                hasDigit = true;
        }

        return hasLetter || hasDigit;
    }

    private static bool IsValidHumanNameWord(string word)
    {
        if (word.Length == 0)
            return false;

        if (!char.IsLetter(word[0]))
            return false;

        if (!EndsWithLetterOrMark(word))
            return false;

        var previousWasSeparator = false;

        for (var i = 0; i < word.Length; i++)
        {
            var ch = word[i];
            if (char.IsLetter(ch))
            {
                previousWasSeparator = false;
                continue;
            }

            if (IsCombiningMark(ch))
                continue;

            if (IsNameSeparator(ch))
            {
                if (i == 0 || i == word.Length - 1 || previousWasSeparator)
                    return false;

                if (!HasLetterAhead(word, i + 1))
                    return false;

                previousWasSeparator = true;
                continue;
            }

            return false;
        }

        return true;
    }

    private static bool HasLetterAhead(string word, int startIndex)
    {
        for (var i = startIndex; i < word.Length; i++)
        {
            if (char.IsLetter(word[i]))
                return true;

            if (!IsCombiningMark(word[i]))
                return false;
        }

        return false;
    }

    private static bool EndsWithLetterOrMark(string word)
    {
        for (var i = word.Length - 1; i >= 0; i--)
        {
            if (char.IsLetter(word[i]))
                return true;

            if (!IsCombiningMark(word[i]))
                return false;
        }

        return false;
    }

    private static bool ContainsLetter(string value)
    {
        foreach (var ch in value)
        {
            if (char.IsLetter(ch))
                return true;
        }

        return false;
    }

    private static bool IsCombiningMark(char ch)
    {
        var category = CharUnicodeInfo.GetUnicodeCategory(ch);
        return category is UnicodeCategory.NonSpacingMark
            or UnicodeCategory.SpacingCombiningMark
            or UnicodeCategory.EnclosingMark;
    }

    private static bool IsNameSeparator(char ch) =>
        ch is '-' or '\'' or '\u2019' or '\u02BC' or '\u2010' or '\u2011' or '\u2013';
}
