namespace OzelYetenekSinavSistemi.Tests.Services;

public sealed class DataTablesLengthMenuTests
{
    [Theory]
    [InlineData("10,25,50,100", new[] { 10, 25, 50, 100 })]
    [InlineData(" 10 , 25 , 50 , 100 ", new[] { 10, 25, 50, 100 })]
    [InlineData("", new[] { 10, 25, 50, 100 })]
    [InlineData("abc", new[] { 10, 25, 50, 100 })]
    [InlineData("0,-1,foo", new[] { 10, 25, 50, 100 })]
    [InlineData("10,10,25", new[] { 10, 25 })]
    public void OysParseDataTablesLengthMenu_Algorithm_ReturnsExpected(string raw, int[] expected)
    {
        Assert.Equal(expected, ParseLengthMenu(raw));
    }

    [Fact]
    public void OysParseDataTablesLengthMenu_DoesNotTreatCsvAsCharacterOptions()
    {
        var parsed = ParseLengthMenu("10,25,50,100");
        Assert.Equal(4, parsed.Count);
        Assert.DoesNotContain(1, parsed);
        Assert.DoesNotContain(0, parsed);
        Assert.DoesNotContain(2, parsed);
        Assert.DoesNotContain(5, parsed);
        Assert.Equal(new[] { 10, 25, 50, 100 }, parsed);
    }

    [Fact]
    public void SiteJs_ExposesParser_AndStripsHtml5LengthMenuBeforeInit()
    {
        var siteJs = File.ReadAllText(FindUnder(
            "src", "OzelYetenekSinavSistemi.Web", "wwwroot", "js", "site.js"));

        Assert.Contains("function oysParseDataTablesLengthMenu", siteJs, StringComparison.Ordinal);
        Assert.Contains("data-oys-length-menu", siteJs, StringComparison.Ordinal);
        Assert.Contains("removeAttr(\"data-length-menu\")", siteJs, StringComparison.Ordinal);
        Assert.Contains("removeAttr(\"data-oys-length-menu\")", siteJs, StringComparison.Ordinal);
        Assert.Contains("removeData(\"lengthMenu\")", siteJs, StringComparison.Ordinal);
        Assert.Contains("options.lengthMenu = [lengths, lengths.map(String)]", siteJs, StringComparison.Ordinal);

        // Ham CSV string'in doğrudan lengthMenu'ye atanmadığına dair sözleşme.
        Assert.DoesNotContain("options.lengthMenu = lengthMenuRaw", siteJs, StringComparison.Ordinal);
        Assert.DoesNotContain("options.lengthMenu = [lengthMenuRaw", siteJs, StringComparison.Ordinal);
    }

    [Fact]
    public void PreferenceManage_UsesOysLengthMenuAttribute_NotDataTablesAutoMappedCsv()
    {
        var manage = File.ReadAllText(FindUnder(
            "src", "OzelYetenekSinavSistemi.Web", "Views",
            "ExamPreferenceOption", "Manage.cshtml"));

        Assert.Contains("data-oys-length-menu=\"10,25,50,100\"", manage, StringComparison.Ordinal);
        Assert.Contains("data-page-length=\"10\"", manage, StringComparison.Ordinal);
        Assert.DoesNotContain("data-length-menu=", manage, StringComparison.Ordinal);
    }

    [Fact]
    public void OtherDataTablesScreens_DoNotRequireOysLengthMenuAttribute()
    {
        var examPeriodIndex = File.ReadAllText(FindUnder(
            "src", "OzelYetenekSinavSistemi.Web", "Views", "ExamPeriod", "Index.cshtml"));
        Assert.Contains("js-datatable", examPeriodIndex, StringComparison.Ordinal);
        // Diğer tablolar varsayılan DataTables length menu kullanır; CSV attribute zorunlu değil.
        Assert.DoesNotContain("data-length-menu=", examPeriodIndex, StringComparison.Ordinal);
    }

    /// <summary>site.js oysParseDataTablesLengthMenu ile aynı algoritma (regresyon aynası).</summary>
    private static IReadOnlyList<int> ParseLengthMenu(string? raw)
    {
        var fallback = new[] { 10, 25, 50, 100 };
        if (raw is null)
            return fallback;

        var text = raw.Trim();
        if (text.Length == 0)
            return fallback;

        var lengths = new List<int>();
        foreach (var part in text.Split(','))
        {
            if (!int.TryParse(part.Trim(), out var n) || n <= 0)
                continue;
            if (!lengths.Contains(n))
                lengths.Add(n);
        }

        return lengths.Count > 0 ? lengths : fallback;
    }

    private static string FindUnder(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(new[] { dir.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException(string.Join(Path.DirectorySeparatorChar, parts));
    }
}
