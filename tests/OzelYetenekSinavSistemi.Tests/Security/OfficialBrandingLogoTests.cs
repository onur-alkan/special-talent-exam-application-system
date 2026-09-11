using System.Net;
using System.Text;
using OzelYetenekSinavSistemi.Application.DTOs.Applications;
using OzelYetenekSinavSistemi.Infrastructure.Services;
using OzelYetenekSinavSistemi.Tests.Validation.Http;
using OzelYetenekSinavSistemi.Web.Infrastructure;

namespace OzelYetenekSinavSistemi.Tests.Security;

public sealed class OfficialBrandingLogoTests
{
    [Fact]
    public void OfficialLogoFile_ExistsUnderWwwrootBranding()
    {
        var path = FindUnder(
            "src",
            "OzelYetenekSinavSistemi.Web",
            "wwwroot",
            "images",
            "branding",
            "oys-logo.png");

        Assert.True(File.Exists(path));
        Assert.True(new FileInfo(path).Length > 0);
    }

    [Fact]
    public void OfficialLogoPartial_UsesAppendVersionLocalPathAndAlt()
    {
        var partial = File.ReadAllText(FindUnder(
            "src",
            "OzelYetenekSinavSistemi.Web",
            "Views",
            "Shared",
            "_OfficialUniversityLogo.cshtml"));

        Assert.Contains("~/images/branding/oys-logo.png", partial, StringComparison.Ordinal);
        Assert.Contains("asp-append-version=\"true\"", partial, StringComparison.Ordinal);
        Assert.Contains("alt=\"@OfficialUniversityBranding.AlternativeText\"", partial, StringComparison.Ordinal);
        Assert.DoesNotContain("http://", partial, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://", partial, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data:image", partial, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("base64", partial, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AuthAndDocumentViews_UseOfficialLogoPartialOnce()
    {
        var authLayout = File.ReadAllText(FindUnder(
            "src", "OzelYetenekSinavSistemi.Web", "Views", "Shared", "_AuthLayout.cshtml"));
        Assert.Contains("_OfficialUniversityLogo", authLayout, StringComparison.Ordinal);
        Assert.DoesNotContain("<university-logo ", authLayout, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(authLayout, "_OfficialUniversityLogo"));

        var verify = File.ReadAllText(FindUnder(
            "src", "OzelYetenekSinavSistemi.Web", "Views", "DocumentVerification", "Verify.cshtml"));
        Assert.Contains("site-verification-official-logo", verify, StringComparison.Ordinal);

        foreach (var relative in new[] { "View.cshtml", "Result.cshtml" })
        {
            var source = File.ReadAllText(FindUnder(
                "src", "OzelYetenekSinavSistemi.Web", "Views", "ExamDocument", relative));
            Assert.Contains("_OfficialUniversityLogo", source, StringComparison.Ordinal);
            Assert.Contains("site-document-official-logo", source, StringComparison.Ordinal);
            Assert.DoesNotContain("<university-logo ", source, StringComparison.Ordinal);
            Assert.Equal(1, CountOccurrences(source, "_OfficialUniversityLogo"));
        }
    }

    [Fact]
    public void Sidebars_KeepCompactUniversityLogoHelper()
    {
        foreach (var relative in new[] { "_AdminSidebar.cshtml", "_CandidateSidebar.cshtml" })
        {
            var source = File.ReadAllText(FindUnder(
                "src", "OzelYetenekSinavSistemi.Web", "Views", "Shared", relative));
            Assert.Contains("<university-logo variant=\"sidebar\">", source, StringComparison.Ordinal);
            Assert.Contains("logo-element", source, StringComparison.Ordinal);
            Assert.DoesNotContain("DÜ ÖYS", source, StringComparison.Ordinal);
            Assert.DoesNotContain("oys-logo.png", source, StringComparison.Ordinal);
            Assert.DoesNotContain("_OfficialUniversityLogo", source, StringComparison.Ordinal);
            Assert.Equal(2, CountOccurrences(source, "<university-logo variant=\"sidebar\">"));
        }
    }

    [Fact]
    public void Sidebars_CollapsedBrandUsesCompactLogoNotPlainText()
    {
        var css = File.ReadAllText(FindUnder(
            "src", "OzelYetenekSinavSistemi.Web", "wwwroot", "css", "site-security.css"));

        Assert.Contains(".logo-element .university-logo--sidebar", css, StringComparison.Ordinal);
        Assert.Contains("body.mini-navbar .logo-element", css, StringComparison.Ordinal);
        Assert.Contains("object-fit: contain", css, StringComparison.Ordinal);
    }

    [Fact]
    public void SiteSecurityCss_DefinesScopedOfficialLogoClasses()
    {
        var css = File.ReadAllText(FindUnder(
            "src", "OzelYetenekSinavSistemi.Web", "wwwroot", "css", "site-security.css"));

        Assert.Contains(".site-auth-official-logo", css, StringComparison.Ordinal);
        Assert.Contains(".site-verification-official-logo", css, StringComparison.Ordinal);
        Assert.Contains(".site-document-official-logo", css, StringComparison.Ordinal);
        Assert.Contains("max-width: 360px", css, StringComparison.Ordinal);
        Assert.Contains("height: auto", css, StringComparison.Ordinal);
    }

    [Fact]
    public void PdfExport_WithWebRootLogo_ProducesValidPdf()
    {
        var webRoot = FindUnder("src", "OzelYetenekSinavSistemi.Web", "wwwroot");
        var bytes = new PdfExportService(webRoot).ExportCandidateResults(
            "Logo Test Sınavı",
            Array.Empty<CandidateApplicationDetail>());

        Assert.True(bytes.Length > 100);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public void PdfExport_WithoutWebRoot_StillProducesValidPdf()
    {
        var bytes = new PdfExportService().ExportCandidateResults(
            "Logo Test Sınavı",
            Array.Empty<CandidateApplicationDetail>());

        Assert.True(bytes.Length > 100);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public void OfficialLogoConstants_MatchAssetPath()
    {
        Assert.Equal("/images/branding/oys-logo.png", OfficialUniversityBranding.PublicPath);
        Assert.Equal("Özel Yetenek Sınavları Başvuru Sistemi", OfficialUniversityBranding.AlternativeText);
        Assert.DoesNotContain(":\\", OfficialUniversityBranding.PublicPath, StringComparison.Ordinal);
        Assert.DoesNotContain("C:", OfficialUniversityBranding.RelativeWebPath, StringComparison.OrdinalIgnoreCase);
    }

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
public sealed class OfficialBrandingLogoHttpTests
{
    private readonly RegisterFormLiveFixture _fixture;

    public OfficialBrandingLogoHttpTests(RegisterFormLiveFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData("/Account/Login", "site-auth-official-logo")]
    [InlineData("/Account/Register", "site-auth-official-logo")]
    [InlineData("/Account/ForgotPassword", "site-auth-official-logo")]
    public async Task AuthGet_RendersSingleOfficialLogoWithAppendVersion(string route, string cssClass)
    {
        var client = ValidationHttpTestSupport.CreateClient(_fixture.Factory, allowAutoRedirect: false);
        var response = await client.GetAsync(route);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(OfficialUniversityBranding.PublicPath, html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(cssClass, html, StringComparison.Ordinal);
        Assert.Contains("alt=\"Özel Yetenek Sınavları Başvuru Sistemi\"", html, StringComparison.Ordinal);
        Assert.Contains("?v=", html, StringComparison.Ordinal);
        Assert.Equal(1, CountLogoSrc(html));
        Assert.DoesNotContain("university-logo--auth", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data:image", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("http://", ExtractLogoSrc(html) ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        if (string.Equals(route, "/Account/Login", StringComparison.Ordinal))
        {
            Assert.Contains("placeholder=\"E-posta adresi veya T.C. kimlik numarası\"", html, StringComparison.Ordinal);
            Assert.Contains("placeholder=\"Güvenlik kodu\"", html, StringComparison.Ordinal);
            Assert.Contains("site-login-title", html, StringComparison.Ordinal);
            Assert.DoesNotContain("ornek@eposta.com", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Yukarıdaki kodu giriniz", html, StringComparison.Ordinal);
            Assert.DoesNotContain("Beni hatırla", html, StringComparison.Ordinal);
            Assert.DoesNotContain("name=\"RememberMe\"", html, StringComparison.Ordinal);
        }

        if (string.Equals(route, "/Account/ForgotPassword", StringComparison.Ordinal))
        {
            Assert.Contains("site-forgot-page", html, StringComparison.Ordinal);
            Assert.Contains("placeholder=\"E-posta adresi veya T.C. kimlik numarası\"", html, StringComparison.Ordinal);
            Assert.Contains("site-forgot-submit", html, StringComparison.Ordinal);
            Assert.DoesNotContain("ornek@eposta.com", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("11 haneli", html, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task ResetPassword_GetWithToken_RendersSingleOfficialLogo()
    {
        var client = ValidationHttpTestSupport.CreateClient(_fixture.Factory, allowAutoRedirect: false);
        var response = await client.GetAsync("/Account/ResetPassword?token=invalid-token-for-logo-check");
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(OfficialUniversityBranding.PublicPath, html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("site-auth-official-logo", html, StringComparison.Ordinal);
        Assert.Contains("alt=\"Özel Yetenek Sınavları Başvuru Sistemi\"", html, StringComparison.Ordinal);
        Assert.Equal(1, CountLogoSrc(html));
    }

    [Fact]
    public async Task DocumentVerification_Verify_UsesVerificationOfficialLogoClass()
    {
        var client = ValidationHttpTestSupport.CreateClient(_fixture.Factory, allowAutoRedirect: false);
        var response = await client.GetAsync("/DocumentVerification/Verify?code=not-a-real-code");
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(OfficialUniversityBranding.PublicPath, html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("site-verification-official-logo", html, StringComparison.Ordinal);
        Assert.Contains("alt=\"Özel Yetenek Sınavları Başvuru Sistemi\"", html, StringComparison.Ordinal);
        Assert.Equal(1, CountLogoSrc(html));
        Assert.DoesNotContain("site-auth-official-logo", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OfficialLogoStaticAsset_ReturnsPng()
    {
        var client = ValidationHttpTestSupport.CreateClient(_fixture.Factory, allowAutoRedirect: false);
        var response = await client.GetAsync(OfficialUniversityBranding.PublicPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 8);
        Assert.Equal(0x89, bytes[0]);
        Assert.Equal(0x50, bytes[1]);
    }

    private static int CountLogoSrc(string html)
    {
        var needle = OfficialUniversityBranding.PublicPath;
        var count = 0;
        var index = 0;
        while ((index = html.IndexOf(needle, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }

    private static string? ExtractLogoSrc(string html)
    {
        var marker = OfficialUniversityBranding.PublicPath;
        var start = html.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return null;

        var quoteStart = html.LastIndexOf('"', start);
        var quoteEnd = html.IndexOf('"', start);
        if (quoteStart < 0 || quoteEnd <= quoteStart)
            return marker;

        return html[(quoteStart + 1)..quoteEnd];
    }
}
