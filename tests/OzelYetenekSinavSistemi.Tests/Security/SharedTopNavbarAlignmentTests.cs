namespace OzelYetenekSinavSistemi.Tests.Security;

public sealed class SharedTopNavbarAlignmentTests
{
    [Fact]
    public void AdminAndCandidateLayouts_UseSameTopNavbarAndLateOverrideStylesheet()
    {
        foreach (var layoutName in new[] { "_AdminLayout.cshtml", "_CandidateLayout.cshtml" })
        {
            var layout = ReadProjectFile(
                "src", "OzelYetenekSinavSistemi.Web", "Views", "Shared", layoutName);

            Assert.Contains("<partial name=\"_TopNavbar\" />", layout, StringComparison.Ordinal);

            var bootstrapIndex = layout.IndexOf(
                "~/vendor/bootstrap/5.3.8/css/bootstrap.min.css", StringComparison.Ordinal);
            var overrideIndex = layout.IndexOf(
                "~/css/site-security.css", StringComparison.Ordinal);
            var publicEditionIndex = layout.IndexOf(
                "~/css/public-edition.css", StringComparison.Ordinal);

            Assert.True(bootstrapIndex >= 0);
            Assert.True(overrideIndex > bootstrapIndex);
            Assert.True(publicEditionIndex > overrideIndex);
            Assert.DoesNotContain("inspinia", layout, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void SharedTopNavbar_UsesFlexComponentClassesForAllThreeRegions()
    {
        var navbar = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Web", "Views", "Shared", "_TopNavbar.cshtml");

        Assert.Contains("oys-topbar", navbar, StringComparison.Ordinal);
        Assert.Contains("oys-topbar__brand", navbar, StringComparison.Ordinal);
        Assert.Contains("oys-topbar__toggle", navbar, StringComparison.Ordinal);
        Assert.Contains("oys-topbar__title", navbar, StringComparison.Ordinal);
        Assert.Contains("oys-topbar__actions", navbar, StringComparison.Ordinal);
        Assert.Contains("oys-topbar__logout", navbar, StringComparison.Ordinal);
        Assert.Contains("minimalize-styl-2", navbar, StringComparison.Ordinal);
        Assert.Contains("navbar-static-top", navbar, StringComparison.Ordinal);
    }

    [Fact]
    public void TopNavbarOverride_CentersRegionsAndNeutralizesThemeFloatMargins()
    {
        var css = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Web", "wwwroot", "css", "site-security.css");

        Assert.Contains(".navbar.navbar-static-top.oys-topbar", css, StringComparison.Ordinal);
        Assert.Contains(".oys-topbar__brand", css, StringComparison.Ordinal);
        Assert.Contains(".oys-topbar__actions", css, StringComparison.Ordinal);
        Assert.Contains("align-items: center;", css, StringComparison.Ordinal);
        Assert.Contains("min-height: 52px;", css, StringComparison.Ordinal);
        Assert.Contains("float: none;", css, StringComparison.Ordinal);
        Assert.Contains("line-height: 1.2;", css, StringComparison.Ordinal);
    }

    [Fact]
    public void CandidateDashboard_HasNoPageSpecificNavbarWorkaround()
    {
        var dashboard = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Web", "Views", "CandidateDashboard", "Index.cshtml");

        Assert.DoesNotContain("navbar", dashboard, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("oys-topbar", dashboard, StringComparison.Ordinal);
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
