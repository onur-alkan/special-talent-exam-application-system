namespace OzelYetenekSinavSistemi.Tests.Security;

public sealed class SharedSidebarLogoutTests
{
    [Theory]
    [InlineData("_CandidateSidebar.cshtml")]
    [InlineData("_AdminSidebar.cshtml")]
    public void SharedSidebars_UseResponsiveLogoutComponent(string partialName)
    {
        var sidebar = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Web", "Views", "Shared", partialName);

        Assert.Contains("class=\"oys-sidebar-logout\"", sidebar, StringComparison.Ordinal);
        Assert.Contains("class=\"oys-sidebar-logout__button\"", sidebar, StringComparison.Ordinal);
        Assert.Contains(
            "class=\"nav-label oys-sidebar-logout__text\"",
            sidebar,
            StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Çıkış Yap\"", sidebar, StringComparison.Ordinal);
        Assert.Contains("title=\"Çıkış Yap\"", sidebar, StringComparison.Ordinal);
        Assert.Contains("aria-hidden=\"true\"", sidebar, StringComparison.Ordinal);
        Assert.DoesNotContain("btn-block", sidebar, StringComparison.Ordinal);
    }

    [Fact]
    public void CandidateSidebar_MenuLinks_HaveHoverAccessibleNames()
    {
        var sidebar = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Web", "Views", "Shared", "_CandidateSidebar.cshtml");

        foreach (var label in new[]
                 {
                     "Ana Sayfa",
                     "Aktif Başvurular",
                     "Başvurularım",
                     "Profilim",
                     "Şifre Değiştir",
                     "Çıkış Yap"
                 })
        {
            Assert.Contains($"title=\"{label}\"", sidebar, StringComparison.Ordinal);
            Assert.Contains($"aria-label=\"{label}\"", sidebar, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AdminSidebar_MenuLinks_HaveHoverAccessibleNames()
    {
        var sidebar = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Web", "Views", "Shared", "_AdminSidebar.cshtml");

        foreach (var label in new[]
                 {
                     "Dashboard",
                     "Sınav Dönemleri",
                     "Tercih Seçenekleri",
                     "Personel Yönetimi",
                     "Başvuru Yöneticileri",
                     "Aday Başvuruları",
                     "Sınav Sonuçları",
                     "Loglar",
                     "Sistem Ayarları",
                     "Çıkış Yap"
                 })
        {
            Assert.Contains($"title=\"{label}\"", sidebar, StringComparison.Ordinal);
            Assert.Contains($"aria-label=\"{label}\"", sidebar, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SidebarCss_ConstrainsButtonAndHidesTextOnlyInCollapsedMode()
    {
        var css = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Web", "wwwroot", "css", "site-security.css");

        Assert.Contains(".oys-sidebar-logout-item", css, StringComparison.Ordinal);
        Assert.Contains("overflow: hidden;", css, StringComparison.Ordinal);
        Assert.Contains(".oys-sidebar-logout__button", css, StringComparison.Ordinal);
        Assert.Contains(
            "body.mini-navbar:not(.fixed-sidebar):not(.canvas-menu) .oys-sidebar-logout",
            css,
            StringComparison.Ordinal);
        Assert.Contains("width: 70px;", css, StringComparison.Ordinal);
        Assert.Contains("max-width: 70px;", css, StringComparison.Ordinal);
        Assert.Contains(
            "body.mini-navbar:not(.fixed-sidebar):not(.canvas-menu) .oys-sidebar-logout__text",
            css,
            StringComparison.Ordinal);
        Assert.Contains("display: none;", css, StringComparison.Ordinal);
    }

    [Fact]
    public void CandidateDashboard_HasNoSidebarSpecificWorkaround()
    {
        var dashboard = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Web", "Views", "CandidateDashboard", "Index.cshtml");

        Assert.DoesNotContain("oys-sidebar-logout", dashboard, StringComparison.Ordinal);
        Assert.DoesNotContain("mini-navbar", dashboard, StringComparison.Ordinal);
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
