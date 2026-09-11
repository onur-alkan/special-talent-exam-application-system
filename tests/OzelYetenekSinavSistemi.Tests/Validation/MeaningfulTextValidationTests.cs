using System.ComponentModel.DataAnnotations;
using OzelYetenekSinavSistemi.Application.Validation;
using OzelYetenekSinavSistemi.Application.ViewModels.ExamPeriods;
using OzelYetenekSinavSistemi.Application.ViewModels.ExamResults;

namespace OzelYetenekSinavSistemi.Tests.Validation;

public sealed class MeaningfulTextValidationTests
{
    [Theory]
    [InlineData("--------")]
    [InlineData("   ")]
    [InlineData("!!!")]
    [InlineData("😀")]
    [InlineData("Ali\u200B")]
    [InlineData("Ali\u202E")]
    [InlineData("\u0007")]
    public void MeaningfulTitle_RejectsInvalidValues(string value) =>
        Assert.False(InputTextRules.IsValidMeaningfulTitle(value, out _));

    [Theory]
    [InlineData("2026 Özel Yetenek Sınavı")]
    [InlineData("Resim-İş Eğitimi")]
    [InlineData("1. Aşama Sınavı")]
    [InlineData("Grafik Tasarım / Ön Eleme")]
    [InlineData("Müzik (Türk Müziği)")]
    [InlineData("  2026   Özel Yetenek Sınavı  ")]
    public void MeaningfulTitle_AcceptsValidTitles(string value) =>
        Assert.True(InputTextRules.IsValidMeaningfulTitle(value, out _));

    [Theory]
    [InlineData("----")]
    [InlineData("!!!")]
    [InlineData("😀")]
    [InlineData("   ")]
    [InlineData("Ali\u200B")]
    public void MeaningfulText_RejectsInvalidValues(string value) =>
        Assert.False(InputTextRules.IsValidRequiredMeaningfulText(value, multiline: false, out _));

    [Theory]
    [InlineData("Bina No: 12, Kat 3")]
    [InlineData("%40 görme engeli")]
    [InlineData("Daha önce güzel sanatlar eğitimi aldım.")]
    public void MeaningfulText_AcceptsValidValues(string value) =>
        Assert.True(InputTextRules.IsValidRequiredMeaningfulText(value, multiline: false, out _));

    [Fact]
    public void MeaningfulText_Multiline_PreservesLineBreaks()
    {
        var input = "Satır 1\r\n\r\nSatır 2";
        Assert.True(InputTextRules.IsValidRequiredMeaningfulText(input, multiline: true, out var normalized));
        Assert.Contains(Environment.NewLine, normalized!);
        Assert.Contains("Satır 1", normalized);
        Assert.Contains("Satır 2", normalized);
    }

    [Fact]
    public void OptionalMeaningfulText_Empty_IsValidAndNull()
    {
        Assert.True(InputTextRules.IsValidOptionalMeaningfulText("   ", multiline: true, out var normalized));
        Assert.Null(normalized);
    }

    [Fact]
    public void ExamPeriodFormViewModel_InvalidTitle_FailsValidation()
    {
        var model = new ExamPeriodFormViewModel
        {
            Title = "--------",
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddDays(10),
            MaxPreferences = 2
        };

        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, true);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(ExamPeriodFormViewModel.Title)));
    }

    [Fact]
    public void PreferenceOption_InvalidName_FailsValidation()
    {
        var model = new PreferenceOptionFormViewModel
        {
            ExamPeriodId = Guid.NewGuid(),
            PreferenceName = "......",
            DisplayOrder = 1
        };

        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, true);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(PreferenceOptionFormViewModel.PreferenceName)));
    }

    [Fact]
    public void ExamResultFormViewModel_InvalidAdminDescription_FailsValidation()
    {
        var model = new ExamResultFormViewModel
        {
            ApplicationId = Guid.NewGuid(),
            AdminDescription = "--------"
        };

        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, true);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(ExamResultFormViewModel.AdminDescription)));
    }

    [Fact]
    public void ExamResultFormViewModel_ValidAdminDescription_PassesValidation()
    {
        var model = new ExamResultFormViewModel
        {
            ApplicationId = Guid.NewGuid(),
            AdminDescription = "Aday portfolyo sunumunu tamamladı."
        };

        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, true);
        Assert.DoesNotContain(results, r => r.MemberNames.Contains(nameof(ExamResultFormViewModel.AdminDescription)));
    }
}
