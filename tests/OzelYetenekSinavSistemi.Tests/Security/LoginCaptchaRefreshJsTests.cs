namespace OzelYetenekSinavSistemi.Tests.Security;

public sealed class LoginCaptchaRefreshJsTests
{
    [Fact]
    public void SiteJs_CaptchaRefresh_PreservesLoginAndPasswordFields()
    {
        var siteJs = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "wwwroot", "js", "site.js");
        var refreshBlock = ExtractBetween(siteJs, "function oysBindCaptchaRefresh", "function oysInitSelect2");
        var findBlock = ExtractBetween(siteJs, "function oysFindCaptchaInput", "function oysSnapshotLoginFormFieldValues");
        var clearBlock = ExtractBetween(siteJs, "function oysClearCaptchaInput", "function oysSetCaptchaStatus");

        Assert.Contains("function oysFindCaptchaInput", siteJs, StringComparison.Ordinal);
        Assert.Contains("function oysSnapshotLoginFormFieldValues", siteJs, StringComparison.Ordinal);
        Assert.Contains("function oysRestoreLoginFormFieldValues", siteJs, StringComparison.Ordinal);
        Assert.Contains("function oysScheduleLoginFormFieldRestore", siteJs, StringComparison.Ordinal);
        Assert.Contains("input.name !== \"CaptchaInput\"", findBlock, StringComparison.Ordinal);
        Assert.Contains("oysSnapshotLoginFormFieldValues(form)", refreshBlock, StringComparison.Ordinal);
        Assert.Contains("oysRestoreLoginFormFieldValues(fieldSnapshot)", refreshBlock, StringComparison.Ordinal);
        Assert.Contains("oysScheduleLoginFormFieldRestore(fieldSnapshot)", refreshBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("resetForm", refreshBlock, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("resetForm", clearBlock, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("input[name='Password']", clearBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("input[type='password']", clearBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("LoginIdentifier", clearBlock, StringComparison.Ordinal);
    }

    [Fact]
    public void SiteJs_CaptchaRefresh_OnlyClearsCaptchaInputValue()
    {
        var siteJs = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "wwwroot", "js", "site.js");
        var clearBlock = ExtractBetween(siteJs, "function oysClearCaptchaInput", "function oysSetCaptchaStatus");

        Assert.Contains("oysFindCaptchaInput(root)", clearBlock, StringComparison.Ordinal);
        Assert.Contains("input.value = \"\"", clearBlock, StringComparison.Ordinal);
        Assert.Contains("data-valmsg-for='CaptchaInput'", clearBlock, StringComparison.Ordinal);
    }

    private static string ExtractBetween(string source, string start, string end)
    {
        var startIndex = source.IndexOf(start, StringComparison.Ordinal);
        var endIndex = source.IndexOf(end, startIndex, StringComparison.Ordinal);
        Assert.True(startIndex >= 0 && endIndex > startIndex);
        return source.Substring(startIndex, endIndex - startIndex);
    }

    private static string ReadProjectFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);
            directory = directory.Parent;
        }

        throw new FileNotFoundException(string.Join(Path.DirectorySeparatorChar, parts));
    }
}
