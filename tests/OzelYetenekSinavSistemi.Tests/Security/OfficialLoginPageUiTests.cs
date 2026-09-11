namespace OzelYetenekSinavSistemi.Tests.Security;

public sealed class OfficialLoginPageUiTests
{
    [Fact]
    public void LoginMarkup_HasInstitutionalStructureWithSimplifiedControls()
    {
        var login = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "Views", "Account", "Login.cshtml");
        var layout = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "Views", "Shared", "_AuthLayout.cshtml");

        Assert.Contains("site-login-brand", login, StringComparison.Ordinal);
        Assert.Contains("site-login-title", login, StringComparison.Ordinal);
        Assert.Contains("Özel Yetenek Sınavları Başvuru Sistemi", login, StringComparison.Ordinal);
        Assert.Contains("site-login-subtitle", login, StringComparison.Ordinal);
        Assert.Contains("Public Portfolio Edition", login, StringComparison.Ordinal);

        Assert.Contains(
            "label asp-for=\"LoginIdentifier\" class=\"sr-only\">E-posta adresi veya T.C. kimlik numarası</label>",
            login,
            StringComparison.Ordinal);
        Assert.Contains("placeholder=\"E-posta adresi veya T.C. kimlik numarası\"", login, StringComparison.Ordinal);
        Assert.DoesNotContain("ornek@eposta.com", login, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("11 haneli", login, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("label asp-for=\"Password\" class=\"sr-only\"", login, StringComparison.Ordinal);
        Assert.Contains("placeholder=\"Parola\"", login, StringComparison.Ordinal);
        Assert.DoesNotContain("Parolanızı giriniz.", login, StringComparison.Ordinal);
        Assert.DoesNotContain("site-login-password-label-row", login, StringComparison.Ordinal);

        Assert.Contains("label asp-for=\"CaptchaInput\" class=\"site-login-label\"", login, StringComparison.Ordinal);
        Assert.Contains("placeholder=\"Güvenlik kodu\"", login, StringComparison.Ordinal);
        Assert.DoesNotContain("Güvenlik kodunu giriniz.", login, StringComparison.Ordinal);
        Assert.DoesNotContain("Yukarıdaki kodu giriniz", login, StringComparison.Ordinal);

        Assert.Contains("site-login-forgot-row", login, StringComparison.Ordinal);
        Assert.Contains("asp-action=\"ForgotPassword\"", login, StringComparison.Ordinal);
        Assert.Contains("login-identifier-help", login, StringComparison.Ordinal);

        Assert.DoesNotContain("RememberMe", login, StringComparison.Ordinal);
        Assert.DoesNotContain("Beni hatırla", login, StringComparison.Ordinal);
        Assert.DoesNotContain("site-login-remember", login, StringComparison.Ordinal);

        Assert.Contains("asp-action=\"Login\"", login, StringComparison.Ordinal);
        Assert.Contains("asp-controller=\"Account\"", login, StringComparison.Ordinal);
        Assert.Contains("method=\"post\"", login, StringComparison.Ordinal);
        Assert.Contains("type=\"submit\"", login, StringComparison.Ordinal);
        Assert.Contains("class=\"btn btn-white block full-width site-login-submit\"", login, StringComparison.Ordinal);

        Assert.Contains("_OfficialUniversityLogo", layout, StringComparison.Ordinal);
        Assert.Contains("oys-logo.png",
            ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "Views", "Shared", "_OfficialUniversityLogo.cshtml"),
            StringComparison.Ordinal);
        Assert.DoesNotContain("<university-logo ", layout, StringComparison.Ordinal);
        Assert.Contains("viewport", layout, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("site-security.css", layout, StringComparison.Ordinal);
        Assert.Contains("asp-append-version=\"true\"", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("display-1", login, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("display-2", login, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("display-3", login, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("display-4", login, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<h1 class=\"site-login-title\">Özel Yetenek Sınavları Başvuru Sistemi</h1>", login, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(login, "<h1 class=\"site-login-title\">"));
        Assert.DoesNotContain("onclick=", login, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script>", login, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("style=", login, StringComparison.OrdinalIgnoreCase);

        var passwordLabelIndex = login.IndexOf("label asp-for=\"Password\"", StringComparison.Ordinal);
        var passwordInputIndex = login.IndexOf("asp-for=\"Password\"", passwordLabelIndex + 1, StringComparison.Ordinal);
        var forgotIndex = login.IndexOf("Parolamı Unuttum", StringComparison.Ordinal);
        var captchaIndex = login.IndexOf("asp-for=\"CaptchaInput\"", StringComparison.Ordinal);
        Assert.True(passwordInputIndex > 0 && forgotIndex > passwordInputIndex && forgotIndex < captchaIndex,
            "Parolamı Unuttum, parola inputundan sonra ve CAPTCHA'dan önce olmalıdır.");
    }

    [Fact]
    public void LoginCss_DefinesInstitutionalCardWithoutGlobalSelectorBreakage()
    {
        var css = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "wwwroot", "css", "site-security.css");

        Assert.Contains(".site-login-page .site-auth-box", css, StringComparison.Ordinal);
        Assert.Contains("max-width: 480px", css, StringComparison.Ordinal);
        Assert.Contains("box-shadow:", css, StringComparison.Ordinal);
        Assert.Contains(".site-login-page h1.site-login-title", css, StringComparison.Ordinal);
        Assert.Contains("font-size: 1.25rem", css, StringComparison.Ordinal);
        Assert.Contains(".site-login-label", css, StringComparison.Ordinal);
        Assert.Contains(".site-login-forgot-row", css, StringComparison.Ordinal);
        Assert.Contains(".site-login-footnote", css, StringComparison.Ordinal);
        Assert.Contains(".site-login-page .site-auth-official-logo", css, StringComparison.Ordinal);
        Assert.Contains(".site-auth-official-logo", css, StringComparison.Ordinal);
        Assert.DoesNotContain(".site-login-remember", css, StringComparison.Ordinal);
        Assert.DoesNotContain(".site-login-password-label-row", css, StringComparison.Ordinal);
        Assert.DoesNotContain("font-size: 170px", css, StringComparison.Ordinal);
        Assert.DoesNotContain("transform: scale", css, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("zoom:", css, StringComparison.OrdinalIgnoreCase);
        var titleRule = ExtractCssRuleBlock(css, ".site-login-page h1.site-login-title");
        Assert.False(string.IsNullOrWhiteSpace(titleRule));
        Assert.DoesNotContain("vw", titleRule, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("vh", titleRule, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("clamp(", titleRule, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@media (max-width: 767.98px)", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 359.98px)", css, StringComparison.Ordinal);
        Assert.Contains("prefers-reduced-motion", css, StringComparison.Ordinal);
        Assert.DoesNotContain("outline: none", css, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LoginTitleCss_UsesScopedPublicEditionRulesWithoutCommercialTheme()
    {
        var css = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "wwwroot", "css", "site-security.css");
        var layout = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "Views", "Shared", "_AuthLayout.cshtml");

        Assert.Contains(".site-login-page h1.site-login-title", css, StringComparison.Ordinal);
        Assert.Contains("font-size: 1.25rem", css, StringComparison.Ordinal);
        Assert.Contains("font-size: 1.1rem", css, StringComparison.Ordinal);
        Assert.Contains("font-size: 1.05rem", css, StringComparison.Ordinal);
        Assert.Contains("~/vendor/bootstrap/5.3.8/css/bootstrap.min.css", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("inspinia", layout, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(Path.Combine(
            FindRepoRoot(), "src", "OzelYetenekSinavSistemi.Web", "wwwroot", "inspinia")));
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OzelYetenekSinavSistemi.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Solution root not found.");
    }

    [Fact]
    public void LoginMarkup_CaptchaAccessibilityAndContractPreserved()
    {
        var login = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "Views", "Account", "Login.cshtml");

        Assert.Contains("id=\"captchaImage\"", login, StringComparison.Ordinal);
        Assert.Contains("id=\"captchaRefresh\"", login, StringComparison.Ordinal);
        Assert.Contains("data-oys-captcha-image", login, StringComparison.Ordinal);
        Assert.Contains("data-oys-captcha-refresh", login, StringComparison.Ordinal);
        Assert.Contains("data-oys-captcha-input", login, StringComparison.Ordinal);
        Assert.Contains("data-oys-captcha-root", login, StringComparison.Ordinal);
        Assert.Contains("data-captcha-url=", login, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Güvenlik kodunu yenile\"", login, StringComparison.Ordinal);
        Assert.Contains("title=\"Güvenlik kodunu yenile\"", login, StringComparison.Ordinal);
        Assert.Contains("alt=\"Güvenlik kodu\"", login, StringComparison.Ordinal);
        Assert.Contains("asp-validation-for=\"LoginIdentifier\"", login, StringComparison.Ordinal);
        Assert.Contains("asp-validation-for=\"Password\"", login, StringComparison.Ordinal);
        Assert.Contains("asp-validation-for=\"CaptchaInput\"", login, StringComparison.Ordinal);
    }

    [Fact]
    public void LoginViewModel_RememberMePropertyRemainsDefaultFalse()
    {
        var model = new Application.ViewModels.Account.LoginViewModel();
        Assert.False(model.RememberMe);

        var source = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Application", "ViewModels", "Account", "LoginViewModel.cs");
        Assert.Contains("public bool RememberMe { get; set; }", source, StringComparison.Ordinal);
    }

    private static string ExtractCssRuleBlock(string css, string selector)
    {
        var start = css.IndexOf(selector, StringComparison.Ordinal);
        if (start < 0)
            return string.Empty;

        var open = css.IndexOf('{', start);
        var close = css.IndexOf('}', open + 1);
        if (open < 0 || close < 0)
            return string.Empty;

        return css.Substring(start, close - start + 1);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
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
