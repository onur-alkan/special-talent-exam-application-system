namespace OzelYetenekSinavSistemi.Tests.Security;

public sealed class AdminSingleSubmitRegressionTests
{
    [Fact]
    public void SiteJs_InvalidForm_DoesNotLockBeforeValidation()
    {
        var siteJs = ReadWebFile("wwwroot", "js", "site.js");
        var block = ExtractBetween(siteJs, "function oysBindSingleSubmitForms", "function oysBindPhotoFileFeedback");

        Assert.Contains("if (!oysIsFormClientValid(form))", block, StringComparison.Ordinal);
        Assert.Contains("oysLockSingleSubmitForm(form)", block, StringComparison.Ordinal);

        var validationIndex = block.IndexOf("oysIsFormClientValid", StringComparison.Ordinal);
        var lockIndex = block.IndexOf("oysLockSingleSubmitForm", StringComparison.Ordinal);
        Assert.True(validationIndex < lockIndex);
    }

    [Fact]
    public void SiteJs_InputTextAdapters_AreRegisteredOnce()
    {
        var siteJs = ReadWebFile("wwwroot", "js", "site.js");
        Assert.Contains("window.oysInputTextAdaptersBound", siteJs, StringComparison.Ordinal);
        Assert.Contains("if (!$.validator.methods.humanname)", siteJs, StringComparison.Ordinal);
    }

    [Fact]
    public void RegisterAndProfileForms_KeepSingleSubmitProtection()
    {
        var register = ReadWebFile("Views", "Account", "Register.cshtml");
        var profile = ReadWebFile("Views", "CandidateProfile", "Index.cshtml");
        Assert.Contains("data-oys-single-submit", register, StringComparison.Ordinal);
        Assert.Contains("data-oys-single-submit", profile, StringComparison.Ordinal);
    }

    private static string ExtractBetween(string source, string start, string end)
    {
        var startIndex = source.IndexOf(start, StringComparison.Ordinal);
        var endIndex = source.IndexOf(end, StringComparison.Ordinal);
        Assert.True(startIndex >= 0);
        Assert.True(endIndex > startIndex);
        return source[startIndex..endIndex];
    }

    private static string ReadWebFile(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(new[] { dir.FullName, "src", "OzelYetenekSinavSistemi.Web" }.Concat(parts).ToArray());
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);
            dir = dir.Parent;
        }

        throw new FileNotFoundException(string.Join('/', parts));
    }
}
