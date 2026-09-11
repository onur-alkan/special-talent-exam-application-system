namespace OzelYetenekSinavSistemi.Tests.Security;

public sealed class OfficialForgotPasswordPageUiTests
{
    [Fact]
    public void ForgotPasswordMarkup_MatchesOfficialAuthContract()
    {
        var view = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "Views", "Account", "ForgotPassword.cshtml");
        var layout = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "Views", "Shared", "_AuthLayout.cshtml");
        var login = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "Views", "Account", "Login.cshtml");

        Assert.Contains("AuthBodyClass\"] = \"site-forgot-page\"", view, StringComparison.Ordinal);
        Assert.Contains("<h1 class=\"site-forgot-title\">Parola Sıfırlama</h1>", view, StringComparison.Ordinal);
        Assert.Contains("placeholder=\"E-posta adresi veya T.C. kimlik numarası\"", view, StringComparison.Ordinal);
        Assert.Contains("asp-validation-for=\"TcNoOrEmail\"", view, StringComparison.Ordinal);
        Assert.Contains("autocomplete=\"username\"", view, StringComparison.Ordinal);
        Assert.Contains("maxlength=\"254\"", view, StringComparison.Ordinal);
        Assert.Contains("class=\"btn btn-primary block full-width site-forgot-submit\"", view, StringComparison.Ordinal);
        Assert.Contains("class=\"btn btn-white block full-width site-forgot-back\"", view, StringComparison.Ordinal);
        Assert.Contains("site-forgot-footnote", view, StringComparison.Ordinal);
        Assert.DoesNotContain("ornek@eposta.com", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("11 haneli", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("display-1", view, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("site-security.css", layout, StringComparison.Ordinal);
        Assert.Contains("asp-append-version=\"true\"", layout, StringComparison.Ordinal);
        Assert.Contains("_OfficialUniversityLogo", layout, StringComparison.Ordinal);

        // Login sayfası bu düzeltmeyle değişmemeli.
        Assert.Contains("site-login-page", login, StringComparison.Ordinal);
        Assert.Contains("placeholder=\"E-posta adresi veya T.C. kimlik numarası\"", login, StringComparison.Ordinal);
    }

    [Fact]
    public void ForgotPasswordCss_DefinesScopedAuthLayoutWithoutCommercialTheme()
    {
        var css = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "wwwroot", "css", "site-security.css");
        var layout = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "Views", "Shared", "_AuthLayout.cshtml");

        Assert.Contains(".site-forgot-page .site-auth-box", css, StringComparison.Ordinal);
        Assert.Contains("max-width: 480px", css, StringComparison.Ordinal);
        Assert.Contains(".site-forgot-page h1.site-forgot-title", css, StringComparison.Ordinal);
        Assert.Contains("font-size: 1.25rem", css, StringComparison.Ordinal);
        Assert.Contains(".site-forgot-page .site-forgot-input", css, StringComparison.Ordinal);
        Assert.Contains("width: 100%", css, StringComparison.Ordinal);
        Assert.Contains(".site-forgot-page .site-auth-official-logo", css, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "transform: scale",
            ExtractCssRuleBlock(css, ".site-forgot-page h1.site-forgot-title"),
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("~/vendor/bootstrap/5.3.8/css/bootstrap.min.css", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("inspinia", layout, StringComparison.OrdinalIgnoreCase);
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
