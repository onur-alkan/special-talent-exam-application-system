using System.Net;

namespace OzelYetenekSinavSistemi.Tests.Validation.Http;

public sealed class LoginCandidateCtaHttpTests(RegisterFormLiveFixture fixture) : RegisterFormLiveTestBase(fixture)
{
    [Fact]
    public async Task Login_Get_ReturnsOkWithProminentCandidateCta()
    {
        var client = ValidationHttpTestSupport.CreateClient(Fixture.Factory, allowAutoRedirect: false);
        var (response, html, token) = await ValidationHttpTestSupport.GetFormAsync(client, "/Account/Login");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.Contains("Aday Başvurusu Oluştur", html, StringComparison.Ordinal);
        Assert.DoesNotContain("İlk kez mi başvuruyorsunuz?", html, StringComparison.Ordinal);
        Assert.Contains("Hesabınız varsa giriş yapın", html, StringComparison.Ordinal);
        Assert.Contains("Giriş Yap", html, StringComparison.Ordinal);
        Assert.Contains("Parolamı Unuttum", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/Account/Register\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/Account/ForgotPassword\"", html, StringComparison.Ordinal);
        Assert.Contains("site-login-register", html, StringComparison.Ordinal);
        Assert.Contains("btn-primary", html, StringComparison.Ordinal);
        Assert.Contains("js-captcha-refresh", html, StringComparison.Ordinal);
        Assert.Contains("name=\"CaptchaInput\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("jquery.validate", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("style=", html, StringComparison.OrdinalIgnoreCase);

        var ctaIndex = html.IndexOf("Aday Başvurusu Oluştur", StringComparison.Ordinal);
        var submitIndex = html.IndexOf("Giriş Yap", StringComparison.Ordinal);
        Assert.True(ctaIndex >= 0 && submitIndex > ctaIndex);
    }

    [Fact]
    public async Task CandidateApplicationCtaTarget_Get_ReturnsOk()
    {
        var client = ValidationHttpTestSupport.CreateClient(Fixture.Factory, allowAutoRedirect: false);
        var response = await client.GetAsync("/Account/Register");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("Access Denied", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("action=\"/Account/Register\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.True(
            html.Contains("Aday Kay", StringComparison.Ordinal)
            || html.Contains("Aday Kayd&#x131;", StringComparison.Ordinal),
            "Register sayfasında aday kayıt başlığı bulunmalıdır.");
    }
}
