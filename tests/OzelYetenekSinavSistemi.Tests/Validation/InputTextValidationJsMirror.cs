using System.Text;
using System.Text.RegularExpressions;
using OzelYetenekSinavSistemi.Application.Validation;

namespace OzelYetenekSinavSistemi.Tests.Validation;

/// <summary>input-text-validation.js algoritmasının C# test aynası — parity doğrulaması için.</summary>
internal static class InputTextValidationJsMirror
{
    private static readonly string[] ZeroWidth = ["\u200B", "\u200C", "\u200D", "\uFEFF", "\u2060", "\u00AD"];
    private static readonly string[] BidiAndFormat =
    [
        "\u200E", "\u200F", "\u202A", "\u202B", "\u202C", "\u202D", "\u202E",
        "\u2066", "\u2067", "\u2068", "\u2069"
    ];
    private static readonly string[] NameSeparators = ["-", "'", "\u2019", "\u02BC", "\u2010", "\u2011", "\u2013"];

    public static bool IsValidHumanName(string? value)
    {
        var normalized = NormalizeSingleLineWhitespace(value);
        if (string.IsNullOrEmpty(normalized))
            return false;
        if (ContainsForbiddenCharacters(normalized))
            return false;
        if (!ContainsLetter(normalized))
            return false;

        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length > 0 && words.All(IsValidHumanNameWord);
    }

    public static bool IsValidMeaningfulTitle(string? value)
    {
        var normalized = NormalizeSingleLineWhitespace(value);
        if (string.IsNullOrEmpty(normalized))
            return false;
        if (ContainsForbiddenCharacters(normalized))
            return false;
        if (!ContainsLetter(normalized))
            return false;
        return HasMeaningfulContent(normalized);
    }

    public static bool IsValidRequiredMeaningfulText(string? value, bool multiline)
    {
        var normalized = multiline ? NormalizeMultilineText(value) : NormalizeSingleLineWhitespace(value);
        if (string.IsNullOrEmpty(normalized))
            return false;
        if (ContainsForbiddenCharacters(normalized))
            return false;
        return HasMeaningfulContent(normalized);
    }

    public static bool IsValidOptionalMeaningfulText(string? value, bool multiline)
    {
        if (value is null || value.Trim().Length == 0)
            return true;
        return IsValidRequiredMeaningfulText(value, multiline);
    }

    private static bool ContainsForbiddenCharacters(string value)
    {
        foreach (var ch in value)
        {
            var code = (int)ch;
            if (code < 32 && ch is not '\r' and not '\n' and not '\t')
                return true;
            if (code == 127)
                return true;
            var s = ch.ToString();
            if (ZeroWidth.Contains(s) || BidiAndFormat.Contains(s))
                return true;
        }

        return false;
    }

    private static bool IsLetter(char ch) => Regex.IsMatch(ch.ToString(), @"\p{L}", RegexOptions.None);

    private static bool IsCombiningMark(char ch) => Regex.IsMatch(ch.ToString(), @"\p{M}", RegexOptions.None);

    private static bool IsNameSeparator(char ch) => NameSeparators.Contains(ch.ToString());

    private static string? NormalizeSingleLineWhitespace(string? value)
    {
        if (value is null)
            return null;
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
            return string.Empty;

        var builder = new StringBuilder();
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

    private static string? NormalizeMultilineText(string? value)
    {
        if (value is null)
            return null;
        var lines = value.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
        var normalized = new List<string>();
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
                continue;
            normalized.Add(NormalizeSingleLineWhitespace(trimmed) ?? string.Empty);
        }

        return normalized.Count == 0 ? string.Empty : string.Join('\n', normalized);
    }

    private static bool ContainsLetter(string value) => Regex.IsMatch(value, @"\p{L}", RegexOptions.None);

    private static bool HasMeaningfulContent(string value)
    {
        var hasLetter = false;
        var hasDigit = false;
        foreach (var ch in value)
        {
            if (IsLetter(ch))
                hasLetter = true;
            else if (char.IsDigit(ch))
                hasDigit = true;
        }

        return hasLetter || hasDigit;
    }

    private static bool IsValidHumanNameWord(string word)
    {
        if (string.IsNullOrEmpty(word))
            return false;
        if (!IsLetter(word[0]))
            return false;
        if (!EndsWithLetterOrMark(word))
            return false;

        var previousWasSeparator = false;
        for (var i = 0; i < word.Length; i++)
        {
            var ch = word[i];
            if (IsLetter(ch))
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

    private static bool EndsWithLetterOrMark(string word)
    {
        for (var i = word.Length - 1; i >= 0; i--)
        {
            var ch = word[i];
            if (IsLetter(ch))
                return true;
            if (!IsCombiningMark(ch))
                return false;
        }

        return false;
    }

    private static bool HasLetterAhead(string word, int startIndex)
    {
        for (var i = startIndex; i < word.Length; i++)
        {
            if (IsLetter(word[i]))
                return true;
            if (!IsCombiningMark(word[i]))
                return false;
        }

        return false;
    }
}
