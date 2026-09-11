// Unicode metin doğrulama — InputTextRules ile parity (sunucu otoritelidir).
(function (global) {
    "use strict";

    var ZERO_WIDTH = ["\u200B", "\u200C", "\u200D", "\uFEFF", "\u2060", "\u00AD"];
    var BIDI_AND_FORMAT = [
        "\u200E", "\u200F", "\u202A", "\u202B", "\u202C", "\u202D", "\u202E",
        "\u2066", "\u2067", "\u2068", "\u2069"
    ];
    var NAME_SEPARATORS = ["-", "'", "\u2019", "\u02BC", "\u2010", "\u2011", "\u2013"];

    function containsForbiddenCharacters(value) {
        if (typeof value !== "string" || value.length === 0) { return false; }
        for (var i = 0; i < value.length; i++) {
            var ch = value.charAt(i);
            var code = value.charCodeAt(i);
            if (code < 32 && ch !== "\r" && ch !== "\n" && ch !== "\t") { return true; }
            if (code === 127) { return true; }
            if (ZERO_WIDTH.indexOf(ch) !== -1) { return true; }
            if (BIDI_AND_FORMAT.indexOf(ch) !== -1) { return true; }
        }
        return false;
    }

    function isLetter(ch) {
        return /\p{L}/u.test(ch);
    }

    function isCombiningMark(ch) {
        return /\p{M}/u.test(ch);
    }

    function isNameSeparator(ch) {
        return NAME_SEPARATORS.indexOf(ch) !== -1;
    }

    function normalizeSingleLineWhitespace(value) {
        if (value == null) { return null; }
        var trimmed = value.trim();
        if (trimmed.length === 0) { return ""; }

        var builder = "";
        var previousWasSpace = false;
        for (var i = 0; i < trimmed.length; i++) {
            var raw = trimmed.charAt(i);
            var ch = raw === "\r" || raw === "\n" || raw === "\t" ? " " : raw;
            if (/\s/.test(ch)) {
                if (!previousWasSpace) {
                    builder += " ";
                    previousWasSpace = true;
                }
                continue;
            }
            previousWasSpace = false;
            builder += ch;
        }
        return builder;
    }

    function normalizeMultilineText(value) {
        if (value == null) { return null; }
        var lines = value.split(/\r\n|\r|\n/);
        var normalized = [];
        for (var i = 0; i < lines.length; i++) {
            var line = lines[i].trim();
            if (line.length === 0) { continue; }
            normalized.push(normalizeSingleLineWhitespace(line));
        }
        if (normalized.length === 0) { return ""; }
        return normalized.join("\n");
    }

    function containsLetter(value) {
        return /\p{L}/u.test(value);
    }

    function endsWithLetterOrMark(word) {
        for (var i = word.length - 1; i >= 0; i--) {
            var ch = word.charAt(i);
            if (isLetter(ch)) { return true; }
            if (!isCombiningMark(ch)) { return false; }
        }
        return false;
    }

    function hasLetterAhead(word, startIndex) {
        for (var i = startIndex; i < word.length; i++) {
            if (isLetter(word.charAt(i))) { return true; }
            if (!isCombiningMark(word.charAt(i))) { return false; }
        }
        return false;
    }

    function isValidHumanNameWord(word) {
        if (!word || word.length === 0) { return false; }
        if (!isLetter(word.charAt(0))) { return false; }
        if (!endsWithLetterOrMark(word)) { return false; }

        var previousWasSeparator = false;
        for (var i = 0; i < word.length; i++) {
            var ch = word.charAt(i);
            if (isLetter(ch)) {
                previousWasSeparator = false;
                continue;
            }
            if (isCombiningMark(ch)) { continue; }
            if (isNameSeparator(ch)) {
                if (i === 0 || i === word.length - 1 || previousWasSeparator) { return false; }
                if (!hasLetterAhead(word, i + 1)) { return false; }
                previousWasSeparator = true;
                continue;
            }
            return false;
        }
        return true;
    }

    function isValidHumanName(value) {
        var normalized = normalizeSingleLineWhitespace(value);
        if (!normalized) { return false; }
        if (containsForbiddenCharacters(normalized)) { return false; }
        if (!containsLetter(normalized)) { return false; }

        var words = normalized.split(/\s+/).filter(function (w) { return w.length > 0; });
        if (words.length === 0) { return false; }
        for (var i = 0; i < words.length; i++) {
            if (!isValidHumanNameWord(words[i])) { return false; }
        }
        return true;
    }

    function hasMeaningfulContent(value) {
        var hasLetter = false;
        var hasDigit = false;
        for (var i = 0; i < value.length; i++) {
            var ch = value.charAt(i);
            if (isLetter(ch)) { hasLetter = true; }
            else if (/\d/.test(ch)) { hasDigit = true; }
        }
        return hasLetter || hasDigit;
    }

    function isValidMeaningfulTitle(value) {
        var normalized = normalizeSingleLineWhitespace(value);
        if (!normalized) { return false; }
        if (containsForbiddenCharacters(normalized)) { return false; }
        if (!containsLetter(normalized)) { return false; }
        return hasMeaningfulContent(normalized);
    }

    function isValidRequiredMeaningfulText(value, multiline) {
        var normalized = multiline ? normalizeMultilineText(value) : normalizeSingleLineWhitespace(value);
        if (!normalized) { return false; }
        if (containsForbiddenCharacters(normalized)) { return false; }
        return hasMeaningfulContent(normalized);
    }

    function isValidOptionalMeaningfulText(value, multiline) {
        if (value == null || (typeof value === "string" && value.trim().length === 0)) {
            return true;
        }
        return isValidRequiredMeaningfulText(value, multiline);
    }

    global.oysInputText = {
        containsForbiddenCharacters: containsForbiddenCharacters,
        normalizeSingleLineWhitespace: normalizeSingleLineWhitespace,
        normalizeMultilineText: normalizeMultilineText,
        isValidHumanName: isValidHumanName,
        isValidMeaningfulTitle: isValidMeaningfulTitle,
        isValidRequiredMeaningfulText: isValidRequiredMeaningfulText,
        isValidOptionalMeaningfulText: isValidOptionalMeaningfulText
    };
})(typeof window !== "undefined" ? window : globalThis);
