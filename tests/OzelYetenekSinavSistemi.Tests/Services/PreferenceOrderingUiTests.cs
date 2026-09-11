using OzelYetenekSinavSistemi.Application.ViewModels.Applications;

namespace OzelYetenekSinavSistemi.Tests.Services;

public sealed class PreferenceOrderingUiTests
{
    [Fact]
    public void ApplyView_UsesCheckboxPreferenceOrderingWithoutSelect()
    {
        var apply = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "Views", "CandidateApplication", "Apply.cshtml");

        Assert.DoesNotContain("<select", apply, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("select2-prefs", apply, StringComparison.Ordinal);
        Assert.DoesNotContain("js-select2-prefs", apply, StringComparison.Ordinal);
        Assert.Contains("data-oys-preference-form", apply, StringComparison.Ordinal);
        Assert.Contains("data-oys-preference-option", apply, StringComparison.Ordinal);
        Assert.Contains("type=\"checkbox\"", apply, StringComparison.Ordinal);
        Assert.Contains("Tercihleriniz", apply, StringComparison.Ordinal);
        Assert.Contains("Seçilen Tercihler", apply, StringComparison.Ordinal);
        Assert.Contains("Henüz tercih seçmediniz.", apply, StringComparison.Ordinal);
        Assert.Contains("Yukarı Taşı", apply, StringComparison.Ordinal);
        Assert.Contains("Aşağı Taşı", apply, StringComparison.Ordinal);
        Assert.Contains("Kaldır", apply, StringComparison.Ordinal);
        Assert.Contains("SelectedPreferenceOptionIds", apply, StringComparison.Ordinal);
        Assert.Contains("data-max-preferences=\"@maxPreferences\"", apply, StringComparison.Ordinal);
        Assert.Contains("En fazla @maxPreferences tercih seçebilirsiniz.", apply, StringComparison.Ordinal);
        Assert.Contains("Başvuruyu Kaydet", apply, StringComparison.Ordinal);
        Assert.Contains("Tercihlerimi Güncelle", apply, StringComparison.Ordinal);
        Assert.Contains("Kaydediliyor...", apply, StringComparison.Ordinal);
        Assert.Contains("Güncelleniyor...", apply, StringComparison.Ordinal);
        Assert.Contains("data-oys-single-submit", apply, StringComparison.Ordinal);
        Assert.Contains("for=\"@inputId\"", apply, StringComparison.Ordinal);
        Assert.Contains("id=\"@inputId\"", apply, StringComparison.Ordinal);
        Assert.Contains("data-oys-preference-live", apply, StringComparison.Ordinal);
        Assert.Contains("aria-live=\"polite\"", apply, StringComparison.Ordinal);
    }

    [Fact]
    public void SiteJs_BindsPreferenceOrderingAndKeepsMoveButtons()
    {
        var siteJs = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "wwwroot", "js", "site.js");

        Assert.Contains("function oysBindPreferenceOrderingForms", siteJs, StringComparison.Ordinal);
        Assert.Contains("oysBindPreferenceOrderingForms", siteJs, StringComparison.Ordinal);
        Assert.Contains("data-oys-preference-move-up", siteJs, StringComparison.Ordinal);
        Assert.Contains("data-oys-preference-move-down", siteJs, StringComparison.Ordinal);
        Assert.Contains("data-oys-preference-remove", siteJs, StringComparison.Ordinal);
        Assert.Contains("SelectedPreferenceOptionIds", siteJs, StringComparison.Ordinal);
        Assert.Contains("En fazla \" + max + \" tercih seçebilirsiniz.", siteJs, StringComparison.Ordinal);
        Assert.Contains("tercihe taşındı.", siteJs, StringComparison.Ordinal);
        Assert.Contains("tercihlerden kaldırıldı.", siteJs, StringComparison.Ordinal);
        Assert.DoesNotContain("js-select2-prefs", siteJs, StringComparison.Ordinal);
        Assert.DoesNotContain("maximumSelectionLength", siteJs, StringComparison.Ordinal);
        Assert.DoesNotContain("drag", siteJs, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateApplicationViewModel_RequiresAtLeastOnePreference()
    {
        var required = typeof(CreateApplicationViewModel)
            .GetProperty(nameof(CreateApplicationViewModel.SelectedPreferenceOptionIds))!
            .GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.RequiredAttribute), inherit: true)
            .Cast<System.ComponentModel.DataAnnotations.RequiredAttribute>()
            .Single();
        var minLength = typeof(CreateApplicationViewModel)
            .GetProperty(nameof(CreateApplicationViewModel.SelectedPreferenceOptionIds))!
            .GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.MinLengthAttribute), inherit: true)
            .Cast<System.ComponentModel.DataAnnotations.MinLengthAttribute>()
            .Single();

        Assert.Equal("En az bir tercih seçiniz.", required.ErrorMessage);
        Assert.Equal(1, minLength.Length);
        Assert.Equal("En az bir tercih seçiniz.", minLength.ErrorMessage);
    }

    [Fact]
    public void PreferenceCss_SupportsSelectedSummaryLayout()
    {
        var css = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "wwwroot", "css", "site-security.css");

        Assert.Contains(".oys-preference-option", css, StringComparison.Ordinal);
        Assert.Contains(".oys-preference-selected-row", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 767px)", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Controller_PreservesPostedSelectionsOnValidationFailure()
    {
        var controller = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Web", "Controllers", "CandidateApplicationController.cs");

        Assert.Contains("BuildFormWithPostedSelectionsAsync", controller, StringComparison.Ordinal);
        Assert.Contains("Tercihleriniz başarıyla kaydedildi.", controller, StringComparison.Ordinal);
        Assert.Contains("Tercihleriniz başarıyla güncellendi.", controller, StringComparison.Ordinal);
        Assert.Contains("ExistingSelectedOptionIds = postedSelections", controller, StringComparison.Ordinal);
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
