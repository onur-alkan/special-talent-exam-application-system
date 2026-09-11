using OzelYetenekSinavSistemi.Web.Infrastructure;

namespace OzelYetenekSinavSistemi.Tests.Security;

/// <summary>
/// Public portfolio edition shell/vendor/branding contracts (replaces commercial theme checks).
/// </summary>
public sealed class PublicEditionShellTests
{
    [Fact]
    public void BootstrapVendorAssets_ExistWithPinnedVersion()
    {
        AssertFile("wwwroot", "vendor", "bootstrap", "5.3.8", "css", "bootstrap.min.css");
        AssertFile("wwwroot", "vendor", "bootstrap", "5.3.8", "js", "bootstrap.bundle.min.js");
        AssertFile("wwwroot", "vendor", "bootstrap", "5.3.8", "LICENSE");
    }

    [Fact]
    public void RequiredOssVendors_ExistWithLicenses()
    {
        AssertFile("wwwroot", "vendor", "jquery", "3.7.1", "js", "jquery.min.js");
        AssertFile("wwwroot", "vendor", "jquery", "3.7.1", "LICENSE");
        AssertFile("wwwroot", "vendor", "select2", "4.0.13", "js", "select2.full.min.js");
        AssertFile("wwwroot", "vendor", "select2", "4.0.13", "LICENSE");
        AssertFile("wwwroot", "vendor", "datatables", "1.13.11", "js", "jquery.dataTables.min.js");
        AssertFile("wwwroot", "vendor", "datatables", "1.13.11", "LICENSE");
        AssertFile("wwwroot", "vendor", "toastr", "2.1.4", "js", "toastr.min.js");
        AssertFile("wwwroot", "vendor", "toastr", "2.1.4", "LICENSE");
        AssertFile("wwwroot", "vendor", "sweetalert2", "11.22.2", "js", "sweetalert2.all.min.js");
        AssertFile("wwwroot", "vendor", "sweetalert2", "11.22.2", "LICENSE");
        AssertFile("wwwroot", "vendor", "jszip", "3.10.1", "js", "jszip.min.js");
        AssertFile("wwwroot", "vendor", "jszip", "3.10.1", "LICENSE");
        AssertFile("wwwroot", "vendor", "pdfmake", "0.2.12", "js", "pdfmake.min.js");
        AssertFile("wwwroot", "vendor", "pdfmake", "0.2.12", "LICENSE");
    }

    [Fact]
    public void Layouts_LoadLocalBootstrapAndVendors_WithoutInspinia()
    {
        foreach (var layout in new[] { "_AdminLayout.cshtml", "_CandidateLayout.cshtml", "_AuthLayout.cshtml" })
        {
            var source = File.ReadAllText(FindUnder(
                "src", "OzelYetenekSinavSistemi.Web", "Views", "Shared", layout));
            Assert.Contains("~/vendor/bootstrap/5.3.8/css/bootstrap.min.css", source, StringComparison.Ordinal);
            Assert.Contains("~/vendor/jquery/3.7.1/js/jquery.min.js", source, StringComparison.Ordinal);
            Assert.DoesNotContain("inspinia", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("cdn.jsdelivr", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("cdnjs.", source, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void NeutralBrandingAssets_ExistAndCommercialAssetsAbsent()
    {
        AssertFile("wwwroot", "images", "branding", "oys-favicon.png");
        AssertFile("wwwroot", "images", "branding", "oys-logo.png");
        Assert.Equal("images/branding/oys-favicon.png", BrowserTabBranding.FaviconRelativeWebPath);
        Assert.Equal("images/branding/oys-logo.png", OfficialUniversityBranding.RelativeWebPath);
        Assert.Equal("ÖYS", BrowserTabBranding.InstitutionalSuffix);
        Assert.DoesNotContain("Düzce", BrowserTabBranding.DefaultTitle, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Düzce", OfficialUniversityBranding.AlternativeText, StringComparison.OrdinalIgnoreCase);

        var brandingDir = FindUnder("src", "OzelYetenekSinavSistemi.Web", "wwwroot", "images", "branding");
        Assert.Empty(Directory.GetFiles(brandingDir, "*duzce*", SearchOption.AllDirectories));
        Assert.False(Directory.Exists(FindOptional("src", "OzelYetenekSinavSistemi.Web", "wwwroot", "inspinia")));
        Assert.False(Directory.Exists(FindOptional("theme-source")));
    }

    [Fact]
    public void FeatureViews_HaveNoCommercialThemeMarkup()
    {
        var viewsRoot = FindUnder("src", "OzelYetenekSinavSistemi.Web", "Views");
        foreach (var file in Directory.EnumerateFiles(viewsRoot, "*.cshtml", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);
            Assert.DoesNotContain("ibox", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/inspinia/", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("theme-source", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("WebAppLayers", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("WrapBootstrap", source, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void AssertFile(params string[] parts)
    {
        var path = FindUnder(new[] { "src", "OzelYetenekSinavSistemi.Web" }.Concat(parts).ToArray());
        Assert.True(File.Exists(path), path);
        Assert.True(new FileInfo(path).Length > 0, path);
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

    private static string? FindOptional(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var path = Path.Combine(new[] { dir.FullName }.Concat(parts).ToArray());
            if (Directory.Exists(path) || File.Exists(path))
                return path;
            if (File.Exists(Path.Combine(dir.FullName, "OzelYetenekSinavSistemi.sln")))
                return Path.Combine(new[] { dir.FullName }.Concat(parts).ToArray());
            dir = dir.Parent;
        }

        return null;
    }
}
