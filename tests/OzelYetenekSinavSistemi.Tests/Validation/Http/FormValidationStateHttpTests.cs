using System.Net;
using System.Text.RegularExpressions;

namespace OzelYetenekSinavSistemi.Tests.Validation.Http;

public sealed class FormValidationStateHttpTests(RegisterFormLiveFixture fixture) : RegisterFormLiveTestBase(fixture)
{
    private static readonly (string Url, string InputName)[] PristineGetForms =
    [
        ("/Account/Login", "LoginIdentifier"),
        ("/Account/Register", "FirstName"),
        ("/Account/ForgotPassword", "TcNoOrEmail"),
        ("/Account/ResetPassword?token=test-token", "NewPassword"),
    ];

    [Theory]
    [MemberData(nameof(PristineGetFormCases))]
    public async Task Form_Get_InitialHtmlIsPristine(string url, string inputName)
    {
        var client = ValidationHttpTestSupport.CreateClient(Fixture.Factory, allowAutoRedirect: false);
        var (response, html, _) = await ValidationHttpTestSupport.GetFormAsync(client, url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(HasInputValidationErrorClass(html, inputName));
        Assert.DoesNotContain("aria-invalid=\"true\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.False(HasVisibleFieldValidationError(html, inputName));
    }

    public static IEnumerable<object[]> PristineGetFormCases =>
        PristineGetForms.Select(f => new object[] { f.Url, f.InputName });

    private static bool HasInputValidationErrorClass(string html, string inputName)
    {
        var pattern = $@"name=""{Regex.Escape(inputName)}""[^>]*class=""[^""]*input-validation-error";
        if (Regex.IsMatch(html, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return true;

        pattern = $@"id=""{Regex.Escape(inputName)}""[^>]*class=""[^""]*input-validation-error";
        return Regex.IsMatch(html, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool HasVisibleFieldValidationError(string html, string inputName)
    {
        var escaped = Regex.Escape(inputName);
        var spanPattern =
            $@"<span[^>]*data-valmsg-for=""{escaped}""[^>]*field-validation-error|<span[^>]*field-validation-error[^>]*data-valmsg-for=""{escaped}""";
        if (Regex.IsMatch(html, spanPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return true;

        var inputPattern =
            $@"<input[^>]*name=""{escaped}""[^>]*input-validation-error|<input[^>]*input-validation-error[^>]*name=""{escaped}""";
        return Regex.IsMatch(html, inputPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    [Fact]
    public void Login_Get_SiteJs_HasFormValidationStatePolicyWithoutInitValid()
    {
        var siteJs = ReadProjectFile("wwwroot", "js", "site.js");

        Assert.Contains("function oysMarkServerValidationForms", siteJs, StringComparison.Ordinal);
        Assert.Contains("function oysResetClientValidationState", siteJs, StringComparison.Ordinal);
        Assert.Contains("function oysApplyFormValidationStatePolicy", siteJs, StringComparison.Ordinal);
        Assert.Contains("function oysBindFormValidationStatePolicy", siteJs, StringComparison.Ordinal);
        Assert.Contains("oysMarkServerValidationForms();", siteJs, StringComparison.Ordinal);
        Assert.Contains("oysApplyFormValidationStatePolicy();", siteJs, StringComparison.Ordinal);
        Assert.DoesNotContain("oysLoginIdentifierPageshowBound", siteJs, StringComparison.Ordinal);

        var loginBlock = ExtractBetween(siteJs, "function oysBindLoginIdentifierValidation", "function oysInitializePage");
        Assert.DoesNotContain("$input.valid();", loginBlock, StringComparison.Ordinal);
        Assert.Contains("oysEnsureUnobtrusiveFormValidator", siteJs, StringComparison.Ordinal);
        Assert.Contains("unobtrusive.parse(form)", siteJs, StringComparison.Ordinal);
        Assert.DoesNotContain("validator.element(input);", siteJs, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_Get_HasRequiredClientMetadataForAllFields()
    {
        var client = ValidationHttpTestSupport.CreateClient(Fixture.Factory, allowAutoRedirect: false);
        var (response, html, _) = await ValidationHttpTestSupport.GetFormAsync(client, "/Account/Login");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("name=\"LoginIdentifier\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"Password\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"CaptchaInput\"", html, StringComparison.Ordinal);
        Assert.Contains("data-valmsg-for=\"LoginIdentifier\"", html, StringComparison.Ordinal);
        Assert.Contains("data-valmsg-for=\"Password\"", html, StringComparison.Ordinal);
        Assert.Contains("data-valmsg-for=\"CaptchaInput\"", html, StringComparison.Ordinal);
        Assert.True(InputHasRequiredClientMetadata(html, "LoginIdentifier"));
        Assert.True(InputHasRequiredClientMetadata(html, "Password"));
        Assert.True(InputHasRequiredClientMetadata(html, "CaptchaInput"));
    }

    private static bool InputHasRequiredClientMetadata(string html, string inputName)
    {
        var pattern = $@"<input\b(?=[^>]*\bname=""{Regex.Escape(inputName)}"")(?=[^>]*\bdata-val-required\b)[^>]*>";
        return Regex.IsMatch(html, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    [Fact]
    public async Task Login_Post_EmptyFields_DoesNotEchoSensitiveValues()
    {
        var client = ValidationHttpTestSupport.CreateClient(Fixture.Factory, allowAutoRedirect: false);
        var (_, _, token) = await ValidationHttpTestSupport.GetFormAsync(client, "/Account/Login");

        using var form = ValidationHttpTestSupport.BuildForm(token!, new Dictionary<string, string>
        {
            ["LoginIdentifier"] = "",
            ["Password"] = "SecretPass1!",
            ["CaptchaInput"] = "1234",
            ["RememberMe"] = "false"
        });

        var response = await client.PostAsync("/Account/Login", form);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("SecretPass1!", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"CaptchaInput\" value=\"1234\"", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_Post_EmptyFields_PreservesServerValidationInHtml()
    {
        var client = ValidationHttpTestSupport.CreateClient(Fixture.Factory, allowAutoRedirect: false);
        var (_, _, token) = await ValidationHttpTestSupport.GetFormAsync(client, "/Account/Login");

        using var form = ValidationHttpTestSupport.BuildForm(token!, new Dictionary<string, string>
        {
            ["LoginIdentifier"] = "",
            ["Password"] = "",
            ["CaptchaInput"] = "",
            ["RememberMe"] = "false"
        });

        var response = await client.PostAsync("/Account/Login", form);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.True(HasVisibleFieldValidationError(html, "LoginIdentifier"));
        Assert.True(HasVisibleFieldValidationError(html, "Password"));
        Assert.True(HasVisibleFieldValidationError(html, "CaptchaInput"));
    }

    [Fact]
    public async Task Login_Post_InvalidCaptcha_PreservesCaptchaFieldError()
    {
        var client = ValidationHttpTestSupport.CreateClient(Fixture.Factory, allowAutoRedirect: false);
        var (_, _, token) = await ValidationHttpTestSupport.GetFormAsync(client, "/Account/Login");
        Assert.False(string.IsNullOrWhiteSpace(token));

        var first = await client.GetAsync("/Account/Captcha?t=1");
        var firstCode = ExtractCaptchaCodeFromSvg(await first.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrWhiteSpace(firstCode));

        await client.GetAsync("/Account/Captcha?t=2");

        using var form = ValidationHttpTestSupport.BuildForm(token!, new Dictionary<string, string>
        {
            ["LoginIdentifier"] = "admin@example.test",
            ["Password"] = "WrongPassword1!",
            ["CaptchaInput"] = firstCode!,
            ["RememberMe"] = "false"
        });

        var response = await client.PostAsync("/Account/Login", form);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("field-validation-error", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-valmsg-for=\"CaptchaInput\"", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_Get_HasValidationScriptsWithoutInitialFieldErrors()
    {
        var client = ValidationHttpTestSupport.CreateClient(Fixture.Factory, allowAutoRedirect: false);
        var (response, html, _) = await ValidationHttpTestSupport.GetFormAsync(client, "/Account/Register");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("jquery.validate", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("site.js", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("validation-summary-errors", html, StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractCaptchaCodeFromSvg(string svg)
    {
        var matches = Regex.Matches(svg, "<text[^>]*>([^<])</text>", RegexOptions.CultureInvariant);
        if (matches.Count == 0)
            return null;

        return string.Concat(matches.Select(m => m.Groups[1].Value));
    }

    private static string ExtractBetween(string source, string start, string end)
    {
        var startIndex = source.IndexOf(start, StringComparison.Ordinal);
        var endIndex = source.IndexOf(end, startIndex, StringComparison.Ordinal);
        if (startIndex < 0 || endIndex <= startIndex)
            throw new InvalidOperationException($"Could not extract block between {start} and {end}");

        return source.Substring(startIndex, endIndex - startIndex);
    }

    private static string ReadProjectFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName, "src", "OzelYetenekSinavSistemi.Web" }.Concat(parts).ToArray());
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);
            directory = directory.Parent;
        }

        throw new FileNotFoundException(string.Join('/', parts));
    }
}
