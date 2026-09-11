using Microsoft.AspNetCore.Mvc;
using OzelYetenekSinavSistemi.Application.Validation;
using OzelYetenekSinavSistemi.Web.Infrastructure;

namespace OzelYetenekSinavSistemi.Tests.Validation;

public sealed class TurkishModelBindingMessageTests
{
    [Theory]
    [InlineData("YGS Puanı", "YGS puanını sayı olarak giriniz.")]
    [InlineData("YgsScore", "YGS puanını sayı olarak giriniz.")]
    [InlineData("Görünüm Sırası", "Görünüm sırasını sayı olarak giriniz.")]
    [InlineData("DisplayOrder", "Görünüm sırasını sayı olarak giriniz.")]
    [InlineData("Maksimum Tercih Sayısı", "Maksimum tercih sayısını sayı olarak giriniz.")]
    [InlineData("MaxPreferences", "Maksimum tercih sayısını sayı olarak giriniz.")]
    [InlineData("Sınav Puanı", "Sınav puanını sayı olarak giriniz.")]
    [InlineData("ExamScore", "Sınav puanını sayı olarak giriniz.")]
    [InlineData("", TurkishModelBindingMessages.GenericNumber)]
    [InlineData("UnknownField", TurkishModelBindingMessages.GenericNumber)]
    public void ValueMustBeANumber_UsesFieldCatalogOrGeneric(string field, string expected)
    {
        Assert.Equal(expected, TurkishModelBindingMessages.ValueMustBeANumber(field));
    }

    [Theory]
    [InlineData("Başlangıç Tarihi", "Başlangıç tarihini kontrol ediniz.")]
    [InlineData("StartDate", "Başlangıç tarihini kontrol ediniz.")]
    [InlineData("Bitiş Tarihi", "Bitiş tarihini kontrol ediniz.")]
    [InlineData("EndDate", "Bitiş tarihini kontrol ediniz.")]
    [InlineData("Pasaport Son Geçerlilik Tarihi", "Pasaport son geçerlilik tarihini kontrol ediniz.")]
    [InlineData("Doğum Tarihi", "Geçerli bir doğum tarihi giriniz.")]
    [InlineData("BirthDate", "Geçerli bir doğum tarihi giriniz.")]
    public void AttemptedValueIsInvalid_UsesDateMessages(string field, string expected)
    {
        Assert.Equal(expected, TurkishModelBindingMessages.AttemptedValueIsInvalid("--", field));
    }

    [Fact]
    public void Catalog_DoesNotExposeEnglishFrameworkPhrases()
    {
        var samples = new[]
        {
            TurkishModelBindingMessages.ValueMustBeANumber("YGS Puanı"),
            TurkishModelBindingMessages.ValueMustBeANumber("DisplayOrder"),
            TurkishModelBindingMessages.AttemptedValueIsInvalid("x", "EndDate"),
            TurkishModelBindingMessages.ValueIsInvalid("StartDate"),
            TurkishModelBindingMessages.UnknownValueIsInvalid("AttendanceStatus"),
            TurkishModelBindingMessages.ValueMustNotBeNull("MaxPreferences"),
            TurkishModelBindingMessages.GenericNumber,
            TurkishModelBindingMessages.GenericValue,
            TurkishModelBindingMessages.GenericDate
        };

        foreach (var sample in samples)
        {
            Assert.DoesNotContain("The field", sample, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("The value", sample, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("must be a number", sample, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("is not valid", sample, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("value is invalid", sample, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("could not be converted", sample, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void MvcOptions_AppliesTurkishModelBindingMessageProvider()
    {
        var options = new MvcOptions();
        TurkishModelBindingMessageConfiguration.Apply(options);

        Assert.Equal(
            "YGS puanını sayı olarak giriniz.",
            options.ModelBindingMessageProvider.ValueMustBeANumberAccessor("YGS Puanı"));
        Assert.Equal(
            "Görünüm sırasını sayı olarak giriniz.",
            options.ModelBindingMessageProvider.ValueMustBeANumberAccessor("DisplayOrder"));
        Assert.Equal(
            "Bitiş tarihini kontrol ediniz.",
            options.ModelBindingMessageProvider.AttemptedValueIsInvalidAccessor("--", "EndDate"));
        Assert.Equal(
            TurkishModelBindingMessages.GenericNumber,
            options.ModelBindingMessageProvider.NonPropertyValueMustBeANumberAccessor());
        Assert.DoesNotContain(
            "must be a number",
            options.ModelBindingMessageProvider.ValueMustBeANumberAccessor("YGS Puanı"),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SiteJs_OverridesEnglishJqueryNumberDefaults()
    {
        var siteJs = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "wwwroot", "js", "site.js");
        Assert.Contains("oysBindDefaultValidationMessages", siteJs, StringComparison.Ordinal);
        Assert.Contains("number: \"Geçerli bir sayı giriniz.\"", siteJs, StringComparison.Ordinal);
        Assert.Contains("email: \"Geçerli bir e-posta adresi giriniz.\"", siteJs, StringComparison.Ordinal);
        Assert.Contains("date: \"Tarih bilgisini kontrol ediniz.\"", siteJs, StringComparison.Ordinal);
        Assert.DoesNotContain("Please enter a valid number.", siteJs, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_WiresTurkishModelBindingConfiguration()
    {
        var program = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "Program.cs");
        Assert.Contains("TurkishModelBindingMessageConfiguration.Apply", program, StringComparison.Ordinal);
        Assert.Contains("TurkishNumericClientModelValidatorProvider", program, StringComparison.Ordinal);
    }

    [Fact]
    public void ProfileAnnotations_HaveTurkishErrorMessagesForEmailAndYgs()
    {
        var update = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Application", "ViewModels", "Profile", "ProfileUpdateViewModel.cs");
        var profile = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Application", "ViewModels", "Profile", "ProfileViewModel.cs");

        foreach (var source in new[] { update, profile })
        {
            Assert.Contains(
                "EmailAddress(ErrorMessage = \"Geçerli bir e-posta adresi giriniz.\")",
                source,
                StringComparison.Ordinal);
            Assert.Contains(
                "Range(0, 560, ErrorMessage = \"YGS puanını 0-560 aralığında giriniz.\")",
                source,
                StringComparison.Ordinal);
            Assert.DoesNotContain("[EmailAddress]", source, StringComparison.Ordinal);
            Assert.DoesNotContain("[Range(0, 560)]", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SelectPlaceholders_UseEmptyValueNotDashDash()
    {
        var files = new[]
        {
            Path.Combine("Views", "Account", "Register.cshtml"),
            Path.Combine("Views", "CandidateProfile", "Index.cshtml"),
            Path.Combine("Views", "ExamPeriodManager", "Manage.cshtml"),
            Path.Combine("Views", "UserManagement", "Create.cshtml")
        };

        foreach (var relative in files)
        {
            var parts = new List<string> { "src", "OzelYetenekSinavSistemi.Web" };
            parts.AddRange(relative.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries));
            var html = ReadProjectFile(parts.ToArray());
            Assert.DoesNotContain("value=\"--\"", html, StringComparison.Ordinal);
            Assert.DoesNotContain("value=\"-\"", html, StringComparison.Ordinal);
        }
    }

    private static string ReadProjectFile(params string[] parts)
    {
        var root = FindRepoRoot();
        return File.ReadAllText(Path.Combine(new[] { root }.Concat(parts).ToArray()));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "OzelYetenekSinavSistemi.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root could not be located.");
    }
}
