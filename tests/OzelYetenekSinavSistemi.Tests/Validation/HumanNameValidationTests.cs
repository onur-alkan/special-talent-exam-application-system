using OzelYetenekSinavSistemi.Application.Validation;

namespace OzelYetenekSinavSistemi.Tests.Validation;

public sealed class HumanNameValidationTests
{
    public static IEnumerable<object[]> InvalidHumanNames =>
    [
        ["--------"], ["........"], ["!!!!"], ["123456"], ["   "], ["\t\t"], ["\n\n"],
        ["😀😀"], ["\u0301\u0301"], ["Ali\u200B"], ["Ali\u200C"], ["Ali\u200D"],
        ["Ali\u2060Veli"], ["Ali\u200EVeli"], ["Ali\u200FVeli"], ["Ali\u202EVeli"],
        ["Ali\u2066Veli"], ["Ali\u2067Veli"], ["Ali\u2068Veli"], ["Ali\u2069Veli"],
        ["Ali\u0000Veli"], ["-Ali"], ["Ali-"], ["'Ali"], ["Ali'"], ["Ali\u2019"],
        ["Ali--Veli"], ["Ali''Veli"], ["Ali - Veli"], ["<script>alert(1)</script>"],
        ["123!!!"],
    ];

    public static IEnumerable<object[]> ValidHumanNames =>
    [
        ["Çağla"], ["Ömer Faruk"], ["Jean-Pierre"], ["D'Angelo"], ["O\u2019Connor"],
        ["José"], ["François"], ["Łukasz"], ["Anne-Marie"], ["Muhammed Ali"],
        ["e\u0301"], ["  Ömer   Faruk  "], ["  Jean-Pierre  "]
    ];

    [Theory]
    [MemberData(nameof(InvalidHumanNames))]
    public void IsValidHumanName_RejectsInvalidValues(string value) =>
        Assert.False(InputTextRules.IsValidHumanName(value, out _));

    [Theory]
    [MemberData(nameof(ValidHumanNames))]
    public void IsValidHumanName_AcceptsValidUnicodeNames(string value)
    {
        Assert.True(InputTextRules.IsValidHumanName(value, out var normalized));
        Assert.False(string.IsNullOrWhiteSpace(normalized));
    }

    [Fact]
    public void NormalizeHumanName_CollapsesInternalWhitespace()
    {
        Assert.Equal("Ömer Faruk", InputTextRules.NormalizeHumanName("  Ömer   Faruk  "));
        Assert.Equal("Jean-Pierre", InputTextRules.NormalizeHumanName("  Jean-Pierre  "));
    }
}
