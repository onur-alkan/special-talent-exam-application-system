using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OzelYetenekSinavSistemi.Web.Infrastructure;

namespace OzelYetenekSinavSistemi.Tests.Security;

public sealed class CspSecurityTests
{
    [Fact]
    public async Task Middleware_SetsStrictCspAndSecurityHeaders()
    {
        var context = new DefaultHttpContext();
        var nonceProvider = new CspNonceProvider();
        var middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask, nonceProvider);

        await middleware.InvokeAsync(context);

        var csp = context.Response.Headers["Content-Security-Policy"].ToString();
        Assert.False(string.IsNullOrWhiteSpace(csp));
        Assert.DoesNotContain("'unsafe-inline'", ExtractScriptSrc(csp), StringComparison.Ordinal);
        Assert.DoesNotContain("'unsafe-eval'", csp, StringComparison.Ordinal);
        Assert.Contains("script-src-attr 'none'", csp, StringComparison.Ordinal);
        Assert.Contains("object-src 'none'", csp, StringComparison.Ordinal);
        Assert.Contains("frame-ancestors 'self'", csp, StringComparison.Ordinal);
        Assert.Contains("'self'", ExtractScriptSrc(csp), StringComparison.Ordinal);
        Assert.DoesNotContain("cdn.", csp, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("http://", csp, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://evil", csp, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fonts.googleapis.com", csp, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fonts.gstatic.com", csp, StringComparison.OrdinalIgnoreCase);

        var styleSrc = ExtractDirective(csp, "style-src");
        Assert.Equal("style-src 'self' 'unsafe-inline'", styleSrc);
        Assert.DoesNotContain("fonts.", styleSrc, StringComparison.OrdinalIgnoreCase);

        var fontSrc = ExtractDirective(csp, "font-src");
        Assert.Equal("font-src 'self' data:", fontSrc);
        Assert.DoesNotContain("fonts.googleapis", fontSrc, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fonts.gstatic", fontSrc, StringComparison.OrdinalIgnoreCase);

        Assert.Equal("nosniff", context.Response.Headers["X-Content-Type-Options"].ToString());
        Assert.Equal("SAMEORIGIN", context.Response.Headers["X-Frame-Options"].ToString());
        Assert.Equal("no-referrer", context.Response.Headers["Referrer-Policy"].ToString());
        Assert.Contains("camera=()", context.Response.Headers["Permissions-Policy"].ToString(), StringComparison.Ordinal);
        Assert.Equal("same-origin", context.Response.Headers["Cross-Origin-Opener-Policy"].ToString());
        Assert.Equal("same-origin", context.Response.Headers["Cross-Origin-Resource-Policy"].ToString());
        Assert.Equal("0", context.Response.Headers["X-XSS-Protection"].ToString());
        Assert.Equal("none", context.Response.Headers["X-Permitted-Cross-Domain-Policies"].ToString());
    }

    [Fact]
    public void BuildCsp_IncludesNonce_AndRejectsInjectionChars()
    {
        var nonce = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        var csp = SecurityHeadersMiddleware.BuildContentSecurityPolicy(nonce);
        Assert.Contains($"'nonce-{nonce}'", csp, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() =>
            SecurityHeadersMiddleware.BuildContentSecurityPolicy("abc\r\ndef"));
    }

    [Fact]
    public void Nonce_SameRequestStable_DifferentRequestsDiffer()
    {
        var provider = new CspNonceProvider();
        var a = new DefaultHttpContext();
        var b = new DefaultHttpContext();

        var a1 = provider.GetNonce(a);
        var a2 = provider.GetNonce(a);
        var b1 = provider.GetNonce(b);

        Assert.Equal(a1, a2);
        Assert.NotEqual(a1, b1);
        Assert.True(IsSafeBase64Nonce(a1));
        Assert.True(IsSafeBase64Nonce(b1));
        Assert.True(Convert.FromBase64String(a1).Length >= 16);
    }

    [Fact]
    public async Task MiddlewareAndTagHelper_ShareSameNonce()
    {
        var httpContext = new DefaultHttpContext();
        var provider = new CspNonceProvider();
        var middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask, provider);
        await middleware.InvokeAsync(httpContext);

        var csp = httpContext.Response.Headers["Content-Security-Policy"].ToString();
        var nonceInHeader = ExtractNonce(csp);

        var accessor = new Mock<IHttpContextAccessor>();
        accessor.SetupGet(a => a.HttpContext).Returns(httpContext);
        var tagHelper = new CspNonceTagHelper(accessor.Object, provider);

        var output = new TagHelperOutput(
            "script",
            new TagHelperAttributeList(),
            (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        var thContext = new TagHelperContext(
            tagName: "script",
            allAttributes: new TagHelperAttributeList(),
            items: new Dictionary<object, object>(),
            uniqueId: "test");

        await tagHelper.ProcessAsync(thContext, output);

        Assert.Equal(nonceInHeader, output.Attributes["nonce"].Value?.ToString());
    }

    [Fact]
    public async Task TagHelper_DoesNotOverwriteExistingNonce()
    {
        var httpContext = new DefaultHttpContext();
        var provider = new CspNonceProvider();
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.SetupGet(a => a.HttpContext).Returns(httpContext);
        var tagHelper = new CspNonceTagHelper(accessor.Object, provider);

        var output = new TagHelperOutput(
            "script",
            new TagHelperAttributeList { new TagHelperAttribute("nonce", "preset") },
            (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        var thContext = new TagHelperContext(
            "script",
            new TagHelperAttributeList { new TagHelperAttribute("nonce", "preset") },
            new Dictionary<object, object>(),
            "t2");

        await tagHelper.ProcessAsync(thContext, output);
        Assert.Equal("preset", output.Attributes["nonce"].Value?.ToString());
    }

    [Fact]
    public void Views_HaveNoInlineEventHandlersOrJavascriptUrls()
    {
        var viewsRoot = FindUnder("src", "OzelYetenekSinavSistemi.Web", "Views");
        foreach (var file in Directory.EnumerateFiles(viewsRoot, "*.cshtml", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("onclick=", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("onchange=", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("oninput=", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("onload=", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("onerror=", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("onsubmit=", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("javascript:", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Notifications_DoNotInterpolateTempDataIntoScript()
    {
        var source = File.ReadAllText(FindUnder("src", "OzelYetenekSinavSistemi.Web", "Views", "Shared", "_Notifications.cshtml"));
        Assert.Contains("js-toast-messages", source, StringComparison.Ordinal);
        Assert.Contains("data-success=", source, StringComparison.Ordinal);
        Assert.DoesNotContain("toastr.success(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Html.Raw", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Json.Serialize", source, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NotificationPayload_IsHtmlAttributeEncoded()
    {
        var payload = "</script><script>alert(1)</script>";
        var encoded = HtmlEncoder.Default.Encode(payload);
        Assert.DoesNotContain("</script>", encoded, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;/script&gt;", encoded, StringComparison.OrdinalIgnoreCase);

        var quotes = "He said \"hi\" \\ and \r\n next";
        var encodedQuotes = HtmlEncoder.Default.Encode(quotes);
        Assert.DoesNotContain("\"hi\"", encodedQuotes, StringComparison.Ordinal);
    }

    [Fact]
    public void SiteJs_UsesSafeToastAndListeners_NoEval()
    {
        var siteJs = File.ReadAllText(FindUnder("src", "OzelYetenekSinavSistemi.Web", "wwwroot", "js", "site.js"));
        Assert.Contains("js-print-document", siteJs, StringComparison.Ordinal);
        Assert.Contains("js-close-window", siteJs, StringComparison.Ordinal);
        Assert.Contains("js-hide-on-image-error", siteJs, StringComparison.Ordinal);
        Assert.Contains("js-captcha-refresh", siteJs, StringComparison.Ordinal);
        Assert.Contains("escapeHtml: true", siteJs, StringComparison.Ordinal);
        Assert.Contains("js-toast-messages", siteJs, StringComparison.Ordinal);
        Assert.Contains("oysIsValidTurkishIdentityNumber", siteJs, StringComparison.Ordinal);
        Assert.Contains("turkishidentity", siteJs, StringComparison.Ordinal);
        Assert.DoesNotContain("eval(", siteJs, StringComparison.Ordinal);
        Assert.DoesNotContain("new Function", siteJs, StringComparison.Ordinal);
        Assert.DoesNotContain(".html(", siteJs, StringComparison.Ordinal);
        Assert.DoesNotContain("innerHTML", siteJs, StringComparison.Ordinal);
    }
    [Fact]
    public void SiteJs_InitializesToastsBeforeOtherBindings_AndSupportsLateLoading()
    {
        var siteJs = File.ReadAllText(
            FindUnder(
                "src",
                "OzelYetenekSinavSistemi.Web",
                "wwwroot",
                "js",
                "site.js"));

        var initializeStart = siteJs.IndexOf(
            "function oysInitializePage()",
            StringComparison.Ordinal);

        var readyStateStart = siteJs.IndexOf(
            "if (document.readyState === \"loading\")",
            StringComparison.Ordinal);

        Assert.True(
            initializeStart >= 0 && readyStateStart > initializeStart,
            "Sayfa başlatma fonksiyonu bulunmalıdır.");

        var initializeBlock = siteJs[initializeStart..readyStateStart];

        var toastCall = initializeBlock.IndexOf(
            "oysShowToastsFromContainer();",
            StringComparison.Ordinal);

        var bindersArray = initializeBlock.IndexOf(
            "var binders = [",
            StringComparison.Ordinal);

        var turkishBinding = initializeBlock.IndexOf(
            "oysBindTurkishIdentityValidation",
            StringComparison.Ordinal);

        Assert.True(
            toastCall >= 0 && bindersArray > toastCall,
            "Bildirimler diğer sayfa bağlayıcılarından önce gösterilmelidir.");

        Assert.True(
            turkishBinding > bindersArray,
            "Kimlik doğrulama bağlayıcısı init listesinde olmalıdır.");

        Assert.Contains("try {", initializeBlock.Replace("\r\n", "\n"), StringComparison.Ordinal);
        Assert.Contains("binders[i]()", initializeBlock, StringComparison.Ordinal);

        // CAPTCHA yenileme, DOMContentLoaded/init hatalarından bağımsız kurulmalıdır.
        var normalized = siteJs.Replace("\r\n", "\n");
        var immediateCaptchaBind = normalized.LastIndexOf(
            "\noysBindCaptchaRefresh();\n",
            StringComparison.Ordinal);
        var normalizedInitStart = normalized.IndexOf(
            "function oysInitializePage()",
            StringComparison.Ordinal);
        Assert.True(
            immediateCaptchaBind > normalizedInitStart,
            "CAPTCHA yenileme document yüklenirken hemen bağlanmalıdır.");

        Assert.Contains(
            "document.readyState === \"loading\"",
            siteJs,
            StringComparison.Ordinal);

        Assert.Contains(
            "document.addEventListener(\"DOMContentLoaded\", oysInitializePage, { once: true });",
            siteJs,
            StringComparison.Ordinal);

        Assert.Contains(
            "else {\n    oysInitializePage();",
            siteJs.Replace("\r\n", "\n"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void DocumentViews_UseSafePrintCloseClasses()
    {
        var view = File.ReadAllText(FindUnder("src", "OzelYetenekSinavSistemi.Web", "Views", "ExamDocument", "View.cshtml"));
        var result = File.ReadAllText(FindUnder("src", "OzelYetenekSinavSistemi.Web", "Views", "ExamDocument", "Result.cshtml"));
        foreach (var text in new[] { view, result })
        {
            Assert.Contains("js-print-document", text, StringComparison.Ordinal);
            Assert.Contains("js-close-window", text, StringComparison.Ordinal);
            Assert.Contains("js-hide-on-image-error", text, StringComparison.Ordinal);
            Assert.DoesNotContain("onclick=", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("javascript:", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Login_CaptchaUsesDataUrlAndRefreshClass()
    {
        var login = File.ReadAllText(FindUnder("src", "OzelYetenekSinavSistemi.Web", "Views", "Account", "Login.cshtml"));
        Assert.Contains("data-captcha-url=", login, StringComparison.Ordinal);
        Assert.Contains("js-captcha-refresh", login, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", login, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Program_RegistersCspNonceProvider()
    {
        var program = File.ReadAllText(FindUnder("src", "OzelYetenekSinavSistemi.Web", "Program.cs"));
        Assert.Contains("ICspNonceProvider", program, StringComparison.Ordinal);
        Assert.Contains("CspNonceProvider", program, StringComparison.Ordinal);
    }

    [Fact]
    public void ViewImports_RegistersWebTagHelpers()
    {
        var imports = File.ReadAllText(FindUnder("src", "OzelYetenekSinavSistemi.Web", "Views", "_ViewImports.cshtml"));
        Assert.Contains("@addTagHelper *, OzelYetenekSinavSistemi.Web", imports, StringComparison.Ordinal);
    }

    [Fact]
    public void Jquery21_NotReferencedFromWwwrootRuntime()
    {
        var webRoot = FindUnder("src", "OzelYetenekSinavSistemi.Web", "wwwroot");
        var jquery21 = Directory.EnumerateFiles(webRoot, "jquery-2.1.1*", SearchOption.AllDirectories).ToList();
        Assert.Empty(jquery21);

        var viewsRoot = FindUnder("src", "OzelYetenekSinavSistemi.Web", "Views");
        foreach (var file in Directory.EnumerateFiles(viewsRoot, "*.cshtml", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("jquery-2.1.1", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void RuntimeViewsAndCss_HaveNoGoogleFontsOrExternalCssImports()
    {
        var viewsRoot = FindUnder("src", "OzelYetenekSinavSistemi.Web", "Views");
        foreach (var file in Directory.EnumerateFiles(viewsRoot, "*.cshtml", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("fonts.googleapis.com", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("fonts.gstatic.com", text, StringComparison.OrdinalIgnoreCase);
        }

        var webRoot = FindUnder("src", "OzelYetenekSinavSistemi.Web", "wwwroot");
        foreach (var file in Directory.EnumerateFiles(webRoot, "*.css", SearchOption.AllDirectories))
        {
            // theme-source runtime'da kullanılmaz; yalnızca wwwroot CSS taranır.
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("fonts.googleapis.com", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("fonts.gstatic.com", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotMatch(
                @"(?i)@import\s+url\(\s*['""]?https?://",
                text);
            Assert.DoesNotMatch(
                @"(?i)@import\s+url\(\s*['""]?//",
                text);
        }
    }

    [Fact]
    public void Csp_StyleAndFontSrcRemainStrict_WithoutGoogleDomains()
    {
        var nonce = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        var csp = SecurityHeadersMiddleware.BuildContentSecurityPolicy(nonce);

        Assert.Equal("style-src 'self' 'unsafe-inline'", ExtractDirective(csp, "style-src"));
        Assert.Equal("font-src 'self' data:", ExtractDirective(csp, "font-src"));
        Assert.DoesNotContain("fonts.googleapis.com", csp, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fonts.gstatic.com", csp, StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractScriptSrc(string csp)
    {
        return ExtractDirective(csp, "script-src");
    }

    private static string ExtractDirective(string csp, string name)
    {
        var start = csp.IndexOf(name + " ", StringComparison.Ordinal);
        Assert.True(start >= 0, $"CSP directive missing: {name}");
        var end = csp.IndexOf(';', start);
        return end > start ? csp[start..end] : csp[start..];
    }

    private static string ExtractNonce(string csp)
    {
        const string marker = "'nonce-";
        var start = csp.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0);
        start += marker.Length;
        var end = csp.IndexOf('\'', start);
        return csp[start..end];
    }

    private static bool IsSafeBase64Nonce(string nonce) =>
        !string.IsNullOrWhiteSpace(nonce)
        && nonce.IndexOfAny(['\r', '\n', '\0', ' ', ';', ',']) < 0
        && Convert.TryFromBase64String(nonce, new Span<byte>(new byte[64]), out _);

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
