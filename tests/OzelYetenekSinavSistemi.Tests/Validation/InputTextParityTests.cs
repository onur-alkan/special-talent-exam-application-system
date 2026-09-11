using OzelYetenekSinavSistemi.Application.Validation;

namespace OzelYetenekSinavSistemi.Tests.Validation;

public sealed class InputTextParityTests
{
    public static IEnumerable<object[]> HumanNameCases =>
    [
        ["--------", false],
        ["   ", false],
        ["😀😀", false],
        ["Jean-Pierre", true],
        ["Ömer Faruk", true],
        ["O\u2019Connor", true],
        ["A\u200BB", false],
        ["\u202Etest", false]
    ];

    public static IEnumerable<object[]> MeaningfulTitleCases =>
    [
        ["--------", false],
        ["😀", false],
        ["2026 Özel Yetenek Sınavı", true],
        ["Grafik Tasarım", true],
        ["Ön Eleme", true],
        ["\u200B", false]
    ];

    public static IEnumerable<object[]> MeaningfulTextCases =>
    [
        ["", true, true],
        ["   ", true, true],
        ["--------", true, false],
        ["😀", true, false],
        ["Satır 1\nSatır 2", true, true],
        ["Açıklama: aday iyi performans gösterdi.", true, true],
        ["\u202Ehidden", true, false]
    ];

    [Theory]
    [MemberData(nameof(HumanNameCases))]
    public void HumanName_ServerAndJsParity(string value, bool expectedValid)
    {
        var serverValid = InputTextRules.IsValidHumanName(value, out _);
        var jsValid = InputTextValidationJsMirror.IsValidHumanName(value);
        Assert.Equal(expectedValid, serverValid);
        Assert.Equal(serverValid, jsValid);
    }

    [Theory]
    [MemberData(nameof(MeaningfulTitleCases))]
    public void MeaningfulTitle_ServerAndJsParity(string value, bool expectedValid)
    {
        var serverValid = InputTextRules.IsValidMeaningfulTitle(value, out _);
        var jsValid = InputTextValidationJsMirror.IsValidMeaningfulTitle(value);
        Assert.Equal(expectedValid, serverValid);
        Assert.Equal(serverValid, jsValid);
    }

    [Theory]
    [MemberData(nameof(MeaningfulTextCases))]
    public void MeaningfulText_ServerAndJsParity(string value, bool optional, bool expectedValid)
    {
        var serverValid = optional
            ? InputTextRules.IsValidOptionalMeaningfulText(value, multiline: true, out _)
            : InputTextRules.IsValidRequiredMeaningfulText(value, multiline: true, out _);
        var jsValid = optional
            ? InputTextValidationJsMirror.IsValidOptionalMeaningfulText(value, multiline: true)
            : InputTextValidationJsMirror.IsValidRequiredMeaningfulText(value, multiline: true);
        Assert.Equal(expectedValid, serverValid);
        Assert.Equal(serverValid, jsValid);
    }
}
