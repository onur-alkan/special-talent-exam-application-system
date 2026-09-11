using System.ComponentModel.DataAnnotations;
using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.Validation;
using OzelYetenekSinavSistemi.Application.ViewModels.Account;
using OzelYetenekSinavSistemi.Application.ViewModels.ExamPeriods;
using OzelYetenekSinavSistemi.Application.ViewModels.ExamResults;
using OzelYetenekSinavSistemi.Application.ViewModels.Profile;
using OzelYetenekSinavSistemi.Domain.Enums;

namespace OzelYetenekSinavSistemi.Tests.Validation;

public sealed class ViewModelValidationTests
{
    private static IList<ValidationResult> Validate(object model)
    {
        var context = new ValidationContext(model);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, context, results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void ExamResult_Attended_WithoutScore_IsInvalid()
    {
        var model = new ExamResultFormViewModel
        {
            ApplicationId = Guid.NewGuid(),
            AttendanceStatus = AttendanceStatus.Attended,
            ExamScore = null
        };

        Assert.NotEmpty(Validate(model));
    }

    [Theory]
    [InlineData(-5)]
    [InlineData(150)]
    public void ExamResult_ScoreOutOfRange_IsInvalid(int score)
    {
        var model = new ExamResultFormViewModel
        {
            ApplicationId = Guid.NewGuid(),
            AttendanceStatus = AttendanceStatus.Attended,
            ExamScore = score
        };

        Assert.NotEmpty(Validate(model));
    }

    [Fact]
    public void ExamResult_NotAttended_WithoutScore_IsValid()
    {
        var model = new ExamResultFormViewModel
        {
            ApplicationId = Guid.NewGuid(),
            AttendanceStatus = AttendanceStatus.NotAttended,
            ExamScore = null
        };

        Assert.Empty(Validate(model));
    }

    [Theory]
    [InlineData(AttendanceStatus.NotAttended)]
    [InlineData(AttendanceStatus.Cancelled)]
    [InlineData(AttendanceStatus.Disqualified)]
    public void ExamResult_NonAttended_WithScore_IsInvalid(AttendanceStatus status)
    {
        var model = new ExamResultFormViewModel
        {
            ApplicationId = Guid.NewGuid(),
            AttendanceStatus = status,
            ExamScore = 70
        };

        Assert.Contains(
            Validate(model),
            result => result.ErrorMessage ==
                "Sınava girmedi, iptal veya diskalifiye durumundaki aday için puan girilemez.");
    }

    [Fact]
    public void ExamResult_UndefinedAttendanceStatus_IsInvalid()
    {
        var model = new ExamResultFormViewModel
        {
            ApplicationId = Guid.NewGuid(),
            AttendanceStatus = (AttendanceStatus)99,
            ExamScore = null
        };

        Assert.Contains(
            Validate(model),
            result => result.ErrorMessage == "Geçerli bir sınava girme durumu seçiniz.");
    }

    [Fact]
    public void ExamPeriod_EndBeforeStart_IsInvalid()
    {
        var model = new ExamPeriodFormViewModel
        {
            Title = "Geçerli Başlık",
            StartDate = DateTime.Today.AddDays(10),
            EndDate = DateTime.Today.AddDays(5),
            MaxPreferences = 3
        };

        Assert.NotEmpty(Validate(model));
    }

    [Fact]
    public void ExamPeriod_Valid_PassesValidation()
    {
        var model = new ExamPeriodFormViewModel
        {
            Title = "Geçerli Başlık",
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddDays(30),
            MaxPreferences = 3
        };

        Assert.Empty(Validate(model));
    }

    [Fact]
    public void Register_HasDisabilityWithoutDetails_IsInvalid()
    {
        var model = ValidRegister();
        model.HasDisability = true;
        model.DisabilityDetails = "   ";

        var results = Validate(model);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(RegisterViewModel.DisabilityDetails)));
        Assert.Contains(results, r => r.ErrorMessage == DisabilityDetailsNormalizer.RequiredMessage);
    }

    [Fact]
    public void Register_HasDisabilityWithDetails_IsValid()
    {
        var model = ValidRegister();
        model.HasDisability = true;
        model.DisabilityDetails = "İşitme engeli";

        Assert.Empty(Validate(model));
    }

    [Fact]
    public void Register_NoDisabilityWithStaleDetails_IsValid()
    {
        var model = ValidRegister();
        model.HasDisability = false;
        model.DisabilityDetails = "Eski metin";

        Assert.Empty(Validate(model));
    }

    [Fact]
    public void Register_WithoutBirthDate_IsInvalid()
    {
        var model = ValidRegister();
        model.BirthDate = null;

        var results = Validate(model);

        Assert.Contains(results, r =>
            r.MemberNames.Contains(nameof(RegisterViewModel.BirthDate))
            && r.ErrorMessage == BirthDateRules.RequiredMessage);
    }

    [Fact]
    public void Register_FutureBirthDate_IsInvalid()
    {
        var model = ValidRegister();
        model.BirthDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(1));

        Assert.Contains(Validate(model), r => r.ErrorMessage == BirthDateRules.FutureMessage);
    }

    [Fact]
    public void Register_TodayBirthDate_IsAccepted()
    {
        var model = ValidRegister();
        model.BirthDate = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        Assert.DoesNotContain(
            Validate(model),
            r => r.MemberNames.Contains(nameof(RegisterViewModel.BirthDate)));
    }

    [Fact]
    public void Register_LeapDay2000_IsAccepted()
    {
        var model = ValidRegister();
        model.BirthDate = new DateOnly(2000, 2, 29);

        Assert.DoesNotContain(
            Validate(model),
            r => r.MemberNames.Contains(nameof(RegisterViewModel.BirthDate)));
    }

    [Fact]
    public void Register_ValidBirthDate_IsAccepted()
    {
        var model = ValidRegister();
        model.BirthDate = new DateOnly(DateTime.UtcNow.Year - 25, 6, 15);

        Assert.DoesNotContain(
            Validate(model),
            r => r.MemberNames.Contains(nameof(RegisterViewModel.BirthDate)));
    }

    [Theory]
    [InlineData(2026, 1906, true)]
    [InlineData(2026, 1905, false)]
    [InlineData(2026, 2027, false)]
    [InlineData(2026, 0, false)]
    public void BirthYearRules_AreDeterministic(int currentYear, int birthYear, bool expected)
    {
        Assert.Equal(expected, BirthYearRules.IsValid(birthYear, currentYear));
    }

    [Fact]
    public void Profile_HasDisabilityWithoutDetails_IsInvalid()
    {
        var model = ValidProfileUpdate();
        model.HasDisability = true;
        model.DisabilityDetails = null;

        var results = Validate(model);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(ProfileUpdateViewModel.DisabilityDetails)));
    }

    [Fact]
    public void Profile_NoDisabilityWithStaleDetails_IsValid()
    {
        var model = ValidProfileUpdate();
        model.HasDisability = false;
        model.DisabilityDetails = "Eski metin";

        Assert.Empty(Validate(model));
    }

    [Fact]
    public void DisabilityDetailsNormalizer_ForStorage_ClearsWhenDisabled()
    {
        Assert.Null(DisabilityDetailsNormalizer.ForStorage(false, "metin"));
        Assert.Equal("metin", DisabilityDetailsNormalizer.ForStorage(true, " metin "));
    }

    private static RegisterViewModel ValidRegister() => new()
    {
        FirstName = "Ali",
        LastName = "Veli",
        IdentityDocumentType = IdentityDocumentType.TurkishIdentityNumber,
        IdentityNumber = "10000000146",
        NationalityCountryCode = "TR",
        BirthDate = new DateOnly(2000, 6, 15),
        Email = "ali@example.com",
        Phone = "5321234567",
        Password = "Passw0rd!",
        ConfirmPassword = "Passw0rd!",
        YgsScore = 250,
        YgsYearId = Guid.NewGuid()
    };

    private static ProfileUpdateViewModel ValidProfileUpdate() => new()
    {
        FirstName = "Ali",
        LastName = "Veli",
        Email = "ali@example.com",
        Phone = "5321234567"
    };
}
