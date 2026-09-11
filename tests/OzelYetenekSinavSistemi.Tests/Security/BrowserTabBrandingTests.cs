using System.Net;
using OzelYetenekSinavSistemi.Tests.Validation.Http;
using OzelYetenekSinavSistemi.Web.Infrastructure;

namespace OzelYetenekSinavSistemi.Tests.Security;

public sealed class BrowserTabBrandingTests
{
    [Theory]
    [InlineData(null, BrowserTabBranding.DefaultTitle)]
    [InlineData("", BrowserTabBranding.DefaultTitle)]
    [InlineData("   ", BrowserTabBranding.DefaultTitle)]
    [InlineData("Giriş", "Giriş - ÖYS")]
    [InlineData("Aday Başvurusu", "Aday Başvurusu - ÖYS")]
    [InlineData("Parolamı Unuttum", "Parolamı Unuttum - ÖYS")]
    [InlineData("Başvurularım", "Başvurularım - ÖYS")]
    [InlineData("Ana Sayfa", "Ana Sayfa - ÖYS")]
    [InlineData("Yönetim Paneli", "Yönetim Paneli - ÖYS")]
    [InlineData("Sınav Dönemleri", "Sınav Dönemleri - ÖYS")]
    [InlineData("Tercih Seçenekleri", "Tercih Seçenekleri - ÖYS")]
    [InlineData("Belge Doğrulama", "Belge Doğrulama - ÖYS")]
    [InlineData("Giriş - ÖYS", "Giriş - ÖYS")]
    [InlineData(BrowserTabBranding.DefaultTitle, BrowserTabBranding.DefaultTitle)]
    public void FormatDocumentTitle_AppliesInstitutionalSuffixWithoutDuplication(string? input, string expected)
    {
        Assert.Equal(expected, BrowserTabBranding.FormatDocumentTitle(input));
    }

    [Fact]
    public void FormatDocumentTitle_NeverEmitsFrameworkOrDevPlaceholders()
    {
        var samples = new[]
        {
            BrowserTabBranding.FormatDocumentTitle("Giriş"),
            BrowserTabBranding.FormatDocumentTitle(null),
            BrowserTabBranding.FormatDocumentTitle("Ana Sayfa")
        };

        foreach (var sample in samples)
        {
            Assert.DoesNotContain("localhost", sample, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ASP.NET", sample, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("OzelYetenekSinavSistemi.Web", sample, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Home Page", sample, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("DÜ ÖYS - DÜ ÖYS", sample, StringComparison.Ordinal);
            Assert.DoesNotContain("<title>", sample, StringComparison.OrdinalIgnoreCase);
            Assert.False(string.IsNullOrWhiteSpace(sample));
            Assert.False(sample.Trim() == "-");
        }
    }

    [Fact]
    public void FaviconAsset_ExistsUnderBrandingFolder()
    {
        var path = FindUnder(
            "src",
            "OzelYetenekSinavSistemi.Web",
            "wwwroot",
            "images",
            "branding",
            "oys-favicon.png");

        Assert.True(File.Exists(path));
        Assert.True(new FileInfo(path).Length > 0);
    }

    [Fact]
    public void Layouts_UseSharedBrowserTabHeadPartial()
    {
        foreach (var layout in new[] { "_AuthLayout.cshtml", "_CandidateLayout.cshtml", "_AdminLayout.cshtml" })
        {
            var source = File.ReadAllText(FindUnder(
                "src", "OzelYetenekSinavSistemi.Web", "Views", "Shared", layout));
            Assert.Contains("_BrowserTabHead", source, StringComparison.Ordinal);
            Assert.DoesNotContain("<title>", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("rel=\"icon\"", source, StringComparison.OrdinalIgnoreCase);
        }

        var head = File.ReadAllText(FindUnder(
            "src", "OzelYetenekSinavSistemi.Web", "Views", "Shared", "_BrowserTabHead.cshtml"));
        Assert.Contains("BrowserTabBranding.FormatDocumentTitle", head, StringComparison.Ordinal);
        Assert.Contains("oys-favicon.png", head, StringComparison.Ordinal);
        Assert.Contains("asp-append-version=\"true\"", head, StringComparison.Ordinal);
        Assert.Contains("type=\"image/png\"", head, StringComparison.Ordinal);
        Assert.Contains("rel=\"icon\"", head, StringComparison.Ordinal);
        Assert.Contains("rel=\"apple-touch-icon\"", head, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(head, "rel=\"icon\""));
        Assert.DoesNotContain("http://", head, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://", head, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data:image", head, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("oys-logo.png", head, StringComparison.Ordinal);
    }

    [Fact]
    public void StandaloneDocumentViews_UseBrowserTabHead()
    {
        foreach (var relative in new[] { "View.cshtml", "Result.cshtml" })
        {
            var source = File.ReadAllText(FindUnder(
                "src", "OzelYetenekSinavSistemi.Web", "Views", "ExamDocument", relative));
            Assert.Contains("_BrowserTabHead", source, StringComparison.Ordinal);
            Assert.DoesNotContain("<title>Sınava Giriş Belgesi - @Model.CandidateNo</title>", source, StringComparison.Ordinal);
            Assert.DoesNotContain("<title>Sınav Sonuç Belgesi - @Model.CandidateNo</title>", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void KeyPages_UseExpectedPageTitles()
    {
        AssertViewTitle("Views", "Account", "Login.cshtml", "Giriş");
        AssertViewTitle("Views", "Account", "Register.cshtml", "Aday Başvurusu");
        AssertViewTitle("Views", "Account", "ForgotPassword.cshtml", "Parolamı Unuttum");
        AssertViewTitle("Views", "Account", "ResetPassword.cshtml", "Parola Sıfırlama");
        AssertViewTitle("Views", "Account", "AccessDenied.cshtml", "Yetkisiz Erişim");
        AssertViewTitle("Views", "DocumentVerification", "Verify.cshtml", "Belge Doğrulama");
        AssertViewTitle("Views", "Home", "NotFound.cshtml", "Sayfa Bulunamadı");
        AssertViewTitle("Views", "Home", "Error.cshtml", "Sistem Hatası");

        AssertFileContains(
            FindUnder("src", "OzelYetenekSinavSistemi.Web", "Controllers", "CandidateDashboardController.cs"),
            "ViewData[\"Title\"] = \"Ana Sayfa\"");
        AssertFileContains(
            FindUnder("src", "OzelYetenekSinavSistemi.Web", "Controllers", "AdminDashboardController.cs"),
            "ViewData[\"Title\"] = \"Yönetim Paneli\"");
        AssertFileContains(
            FindUnder("src", "OzelYetenekSinavSistemi.Web", "Controllers", "ExamPeriodController.cs"),
            "ViewData[\"Title\"] = \"Sınav Dönemleri\"");
        AssertFileContains(
            FindUnder("src", "OzelYetenekSinavSistemi.Web", "Controllers", "ExamPreferenceOptionController.cs"),
            "ViewData[\"Title\"] = \"Tercih Seçenekleri\"");
        AssertFileContains(
            FindUnder("src", "OzelYetenekSinavSistemi.Web", "Controllers", "AuditLogController.cs"),
            "ViewData[\"Title\"] = \"Sistem Logları\"");
    }

    private static void AssertViewTitle(string folder1, string folder2, string fileName, string expected)
    {
        var source = File.ReadAllText(FindUnder(
            "src", "OzelYetenekSinavSistemi.Web", folder1, folder2, fileName));
        Assert.Contains($"ViewData[\"Title\"] = \"{expected}\"", source, StringComparison.Ordinal);
    }

    private static void AssertFileContains(string path, string needle)
        => Assert.Contains(needle, File.ReadAllText(path), StringComparison.Ordinal);

    private static int CountOccurrences(string source, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }

    private static string FindUnder(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var path = Path.Combine(new[] { dir.FullName }.Concat(parts).ToArray());
            if (Directory.Exists(path) || File.Exists(path))
                return path;
            dir = dir.Parent;
        }

        throw new FileNotFoundException(string.Join(Path.DirectorySeparatorChar, parts));
    }
}

[Collection(nameof(RegisterFormLiveCollection))]
public sealed class BrowserTabBrandingHttpTests
{
    private readonly RegisterFormLiveFixture _fixture;

    public BrowserTabBrandingHttpTests(RegisterFormLiveFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData("/Account/Login", "Giriş - ÖYS")]
    [InlineData("/Account/Register", "Aday Başvurusu - ÖYS")]
    [InlineData("/Account/ForgotPassword", "Parolamı Unuttum - ÖYS")]
    [InlineData("/DocumentVerification/Verify?code=x", "Belge Doğrulama - ÖYS")]
    public async Task PublicPages_RenderInstitutionalTitleAndSingleFavicon(string route, string expectedTitle)
    {
        var client = ValidationHttpTestSupport.CreateClient(_fixture.Factory, allowAutoRedirect: false);
        var response = await client.GetAsync(route);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains($"<title>{expectedTitle}</title>", html, StringComparison.Ordinal);
        Assert.Contains(BrowserTabBranding.FaviconPublicPath, html, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, CountOccurrences(html, "rel=\"icon\""));
        Assert.DoesNotContain("localhost", ExtractTitle(html) ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ASP.NET", ExtractTitle(html) ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data:image", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FaviconAsset_ReturnsPng()
    {
        var client = ValidationHttpTestSupport.CreateClient(_fixture.Factory, allowAutoRedirect: false);
        var response = await client.GetAsync(BrowserTabBranding.FaviconPublicPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 8);
        Assert.Equal(0x89, bytes[0]);
        Assert.Equal(0x50, bytes[1]);
    }

    private static string? ExtractTitle(string html)
    {
        const string open = "<title>";
        const string close = "</title>";
        var start = html.IndexOf(open, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return null;
        start += open.Length;
        var end = html.IndexOf(close, start, StringComparison.OrdinalIgnoreCase);
        return end < 0 ? null : html[start..end];
    }

    private static int CountOccurrences(string source, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(needle, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }
}
