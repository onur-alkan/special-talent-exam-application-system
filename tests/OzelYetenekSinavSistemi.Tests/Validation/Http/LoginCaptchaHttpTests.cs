using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace OzelYetenekSinavSistemi.Tests.Validation.Http;

public sealed class LoginCaptchaHttpTests(RegisterFormLiveFixture fixture) : RegisterFormLiveTestBase(fixture)
{
    [Fact]
    public async Task Captcha_Get_ReturnsUncachedSvgWithoutPlaintextLeak()
    {
        var client = ValidationHttpTestSupport.CreateClient(Fixture.Factory, allowAutoRedirect: false);
        await client.GetAsync("/Account/Login");

        var response = await client.GetAsync("/Account/Captcha");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        var body = Encoding.UTF8.GetString(bytes);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/svg+xml", response.Content.Headers.ContentType?.MediaType);
        Assert.False(string.IsNullOrWhiteSpace(response.Headers.CacheControl?.ToString()));
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no-cache", response.Headers.CacheControl.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.True(bytes.Length > 0);
        Assert.StartsWith("<svg", body.TrimStart(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LoginCaptchaCode", body, StringComparison.OrdinalIgnoreCase);
        AssertSensitiveHeadersDoNotLeakCode(response.Headers, response.Content.Headers);
    }

    [Fact]
    public async Task Captcha_ConsecutiveRefresh_ProducesDifferentImagesAndInvalidatesPreviousCode()
    {
        var client = ValidationHttpTestSupport.CreateClient(Fixture.Factory, allowAutoRedirect: false);
        var (_, loginHtml, token) = await ValidationHttpTestSupport.GetFormAsync(client, "/Account/Login");
        Assert.False(string.IsNullOrWhiteSpace(token));

        var first = await client.GetAsync("/Account/Captcha?t=1");
        var firstBytes = await first.Content.ReadAsByteArrayAsync();
        var firstCode = ExtractCaptchaCodeFromSvg(Encoding.UTF8.GetString(firstBytes));
        Assert.False(string.IsNullOrWhiteSpace(firstCode));

        var second = await client.GetAsync("/Account/Captcha?t=2");
        var secondBytes = await second.Content.ReadAsByteArrayAsync();
        var secondCode = ExtractCaptchaCodeFromSvg(Encoding.UTF8.GetString(secondBytes));
        Assert.False(string.IsNullOrWhiteSpace(secondCode));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.NotEqual(Convert.ToHexString(SHA256.HashData(firstBytes)), Convert.ToHexString(SHA256.HashData(secondBytes)));
        Assert.NotEqual(firstCode, secondCode);

        using var staleForm = ValidationHttpTestSupport.BuildForm(token!, new Dictionary<string, string>
        {
            ["LoginIdentifier"] = "admin@example.test",
            ["Password"] = "WrongPassword1!",
            ["CaptchaInput"] = firstCode!,
            ["RememberMe"] = "false"
        });
        var staleLogin = await client.PostAsync("/Account/Login", staleForm);
        var staleBody = await staleLogin.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, staleLogin.StatusCode);
        Assert.Null(staleLogin.Headers.Location);
        Assert.Contains("data-valmsg-for=\"CaptchaInput\"", staleBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("field-validation-error", staleBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_Get_HasRefinedUxAndCaptchaRefreshMarkup()
    {
        var client = ValidationHttpTestSupport.CreateClient(Fixture.Factory, allowAutoRedirect: false);
        var (response, html, _) = await ValidationHttpTestSupport.GetFormAsync(client, "/Account/Login");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Aday Başvurusu Oluştur", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/Account/Register\"", html, StringComparison.Ordinal);
        Assert.Contains("Hesabınız varsa giriş yapın", html, StringComparison.Ordinal);
        Assert.Contains("Parolamı Unuttum", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/Account/ForgotPassword\"", html, StringComparison.Ordinal);
        Assert.Contains("placeholder=\"E-posta adresi veya T.C. kimlik numarası\"", html, StringComparison.Ordinal);
        Assert.Contains("placeholder=\"Parola\"", html, StringComparison.Ordinal);
        Assert.Contains("placeholder=\"Güvenlik kodu\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Beni hatırla", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"RememberMe\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("ornek@eposta.com", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-oys-captcha-refresh", html, StringComparison.Ordinal);
        Assert.Contains("type=\"button\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Güvenlik kodunu yenile\"", html, StringComparison.Ordinal);
        Assert.Contains("site.js", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("İlk kez mi başvuruyorsunuz?", html, StringComparison.Ordinal);
        Assert.DoesNotContain("onclick=", html, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertSensitiveHeadersDoNotLeakCode(HttpResponseHeaders headers, HttpContentHeaders contentHeaders)
    {
        foreach (var header in headers.Concat(contentHeaders))
        {
            var joined = string.Join(",", header.Value);
            Assert.DoesNotContain("LoginCaptchaCode", joined, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string? ExtractCaptchaCodeFromSvg(string svg)
    {
        var matches = Regex.Matches(svg, "<text[^>]*>([^<])</text>", RegexOptions.CultureInvariant);
        if (matches.Count == 0)
            return null;

        var chars = matches.Select(m => m.Groups[1].Value).ToArray();
        return string.Concat(chars);
    }
}
