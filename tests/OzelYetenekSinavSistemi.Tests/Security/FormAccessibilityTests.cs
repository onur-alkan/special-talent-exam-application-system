using System.Text.RegularExpressions;
using OzelYetenekSinavSistemi.Web.Infrastructure;

namespace OzelYetenekSinavSistemi.Tests.Security;

public sealed class FormAccessibilityTests
{
    private static readonly Regex InputTagRegex = new(
        @"<(input|select|textarea)\b([^>]*)>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AttributeRegex = new(
        @"(?<name>[\w:-]+)\s*=\s*(?:""(?<q>[^""]*)""|'(?<q>[^']*)'|(?<q>[^\s>]+))",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex LabelAspForRegex = new(
        @"<label\b[^>]*\basp-for\s*=\s*[""'](?<for>[^""']+)[""'][^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex LabelForRegex = new(
        @"<label\b[^>]*\bfor\s*=\s*[""'](?<for>[^""']+)[""'][^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    [Fact]
    public void Login_InputsHaveAssociatedLabelsOrAccessibleNames()
    {
        var login = File.ReadAllText(FindUnder("src", "OzelYetenekSinavSistemi.Web", "Views", "Account", "Login.cshtml"));

        Assert.Contains("asp-for=\"LoginIdentifier\"", login, StringComparison.Ordinal);
        Assert.Contains("label asp-for=\"LoginIdentifier\"", login, StringComparison.Ordinal);
        Assert.Contains("asp-for=\"Password\"", login, StringComparison.Ordinal);
        Assert.Contains("label asp-for=\"Password\"", login, StringComparison.Ordinal);
        Assert.Contains("label asp-for=\"Password\" class=\"sr-only\"", login, StringComparison.Ordinal);
        Assert.Contains("asp-for=\"CaptchaInput\"", login, StringComparison.Ordinal);
        Assert.Contains("label asp-for=\"CaptchaInput\"", login, StringComparison.Ordinal);
        Assert.DoesNotContain("asp-for=\"RememberMe\"", login, StringComparison.Ordinal);
        Assert.DoesNotContain("Beni hatırla", login, StringComparison.Ordinal);
    }

    [Fact]
    public void Login_RememberMeControlIsNotRendered_ButViewModelPropertyRemains()
    {
        var login = File.ReadAllText(FindUnder("src", "OzelYetenekSinavSistemi.Web", "Views", "Account", "Login.cshtml"));
        var viewModel = File.ReadAllText(FindUnder(
            "src", "OzelYetenekSinavSistemi.Application", "ViewModels", "Account", "LoginViewModel.cs"));

        Assert.DoesNotContain("RememberMe", login, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"RememberMe\"", login, StringComparison.Ordinal);
        Assert.DoesNotContain("Beni hatırla", login, StringComparison.Ordinal);
        Assert.Contains("public bool RememberMe { get; set; }", viewModel, StringComparison.Ordinal);
        Assert.False(new OzelYetenekSinavSistemi.Application.ViewModels.Account.LoginViewModel().RememberMe);
    }

    [Fact]
    public void Login_CaptchaRefreshHasAriaLabel()
    {
        var login = File.ReadAllText(FindUnder("src", "OzelYetenekSinavSistemi.Web", "Views", "Account", "Login.cshtml"));
        Assert.Contains("js-captcha-refresh", login, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Güvenlik kodunu yenile\"", login, StringComparison.Ordinal);
        Assert.Contains("alt=\"Güvenlik kodu\"", login, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeViews_HaveNoUnlabeledFormFields()
    {
        var viewsRoot = FindUnder("src", "OzelYetenekSinavSistemi.Web", "Views");
        var violations = new List<string>();

        foreach (var file in Directory.EnumerateFiles(viewsRoot, "*.cshtml", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            var relative = Path.GetRelativePath(viewsRoot, file).Replace('\\', '/');
            var labeledAspFor = LabelAspForRegex.Matches(text)
                .Select(m => m.Groups["for"].Value)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var labeledIds = LabelForRegex.Matches(text)
                .Select(m => m.Groups["for"].Value)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (Match match in InputTagRegex.Matches(text))
            {
                var tag = match.Groups[1].Value;
                var attrs = ParseAttributes(match.Groups[2].Value);

                if (IsExemptControl(tag, attrs))
                    continue;

                if (HasAccessibleName(attrs, labeledAspFor, labeledIds))
                    continue;

                // Wrapping <label>...<input>...</label> — input sits between label open and close.
                if (IsWrappedByLabel(text, match.Index))
                    continue;

                var name = attrs.GetValueOrDefault("asp-for")
                           ?? attrs.GetValueOrDefault("name")
                           ?? attrs.GetValueOrDefault("id")
                           ?? "(unnamed)";
                violations.Add($"{relative}: <{tag}> name/asp-for/id={name}");
            }
        }

        Assert.True(
            violations.Count == 0,
            "Label'sız form alanları:\n" + string.Join("\n", violations));
    }

    [Fact]
    public void Csp_Unchanged_StyleAndFontSrcStrict()
    {
        var nonce = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        var csp = SecurityHeadersMiddleware.BuildContentSecurityPolicy(nonce);

        Assert.Contains("style-src 'self' 'unsafe-inline'", csp, StringComparison.Ordinal);
        Assert.Contains("font-src 'self' data:", csp, StringComparison.Ordinal);
        Assert.DoesNotContain("fonts.googleapis.com", csp, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fonts.gstatic.com", csp, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsExemptControl(string tag, Dictionary<string, string> attrs)
    {
        if (!string.Equals(tag, "input", StringComparison.OrdinalIgnoreCase))
            return false;

        var type = attrs.GetValueOrDefault("type") ?? "text";
        if (type.Equals("hidden", StringComparison.OrdinalIgnoreCase)
            || type.Equals("submit", StringComparison.OrdinalIgnoreCase)
            || type.Equals("button", StringComparison.OrdinalIgnoreCase)
            || type.Equals("reset", StringComparison.OrdinalIgnoreCase)
            || type.Equals("image", StringComparison.OrdinalIgnoreCase))
            return true;

        var name = attrs.GetValueOrDefault("name") ?? string.Empty;
        if (name.Contains("RequestVerificationToken", StringComparison.OrdinalIgnoreCase)
            || name.Equals("__RequestVerificationToken", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static bool HasAccessibleName(
        Dictionary<string, string> attrs,
        HashSet<string> labeledAspFor,
        HashSet<string> labeledIds)
    {
        if (attrs.ContainsKey("aria-label") || attrs.ContainsKey("aria-labelledby"))
            return true;

        if (attrs.TryGetValue("asp-for", out var aspFor) && labeledAspFor.Contains(aspFor))
            return true;

        if (attrs.TryGetValue("id", out var id))
        {
            if (labeledIds.Contains(id))
                return true;

            // Razor `@valueId` / interpolated ids still declare for="@valueId" pairing in source.
            if (labeledIds.Any(forId => forId.Contains(id, StringComparison.OrdinalIgnoreCase)
                                        || id.Contains(forId, StringComparison.OrdinalIgnoreCase)))
                return true;
        }

        if (attrs.TryGetValue("name", out var name) && labeledIds.Contains(name))
            return true;

        return false;
    }

    private static bool IsWrappedByLabel(string text, int inputIndex)
    {
        var before = text[..inputIndex];
        var lastOpen = before.LastIndexOf("<label", StringComparison.OrdinalIgnoreCase);
        if (lastOpen < 0)
            return false;

        var afterOpen = before[lastOpen..];
        if (afterOpen.Contains("</label>", StringComparison.OrdinalIgnoreCase))
            return false;

        var after = text[inputIndex..];
        var close = after.IndexOf("</label>", StringComparison.OrdinalIgnoreCase);
        var nextOpen = after.IndexOf("<label", StringComparison.OrdinalIgnoreCase);
        return close >= 0 && (nextOpen < 0 || close < nextOpen);
    }

    private static Dictionary<string, string> ParseAttributes(string attributeText)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in AttributeRegex.Matches(attributeText))
            result[match.Groups["name"].Value] = match.Groups["q"].Value;
        return result;
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
