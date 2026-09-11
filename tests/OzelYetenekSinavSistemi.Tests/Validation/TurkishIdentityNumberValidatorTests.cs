using System.ComponentModel.DataAnnotations;
using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.Services;
using OzelYetenekSinavSistemi.Application.Validation;
using OzelYetenekSinavSistemi.Application.ViewModels.Account;
using OzelYetenekSinavSistemi.Application.ViewModels.UserManagement;

namespace OzelYetenekSinavSistemi.Tests.Validation;

public sealed class TurkishIdentityNumberValidatorTests
{
    private readonly TurkishIdentityNumberValidator _sut = new();

    [Theory]
    [InlineData("10000000146")]
    [InlineData("19191919190")]
    [InlineData("11111111110")]
    public void IsValid_AlgorithmicallyValid_ReturnsTrue(string tcNo)
    {
        Assert.True(_sut.IsValid(tcNo));
        Assert.True(TurkishIdentityNumber.IsValid(tcNo));
    }

    [Theory]
    [InlineData("12345678901")]
    [InlineData("11111111112")]
    [InlineData("00000000000")]
    [InlineData("01234567890")]
    public void IsValid_AlgorithmicallyInvalid_ReturnsFalse(string tcNo)
    {
        Assert.False(_sut.IsValid(tcNo));
        Assert.False(TurkishIdentityNumber.IsValid(tcNo));
    }

    [Theory]
    [InlineData("1234567890")]
    [InlineData("123456789012")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void IsValid_WrongLengthOrEmpty_ReturnsFalse(string? tcNo)
    {
        Assert.False(_sut.IsValid(tcNo));
    }

    [Theory]
    [InlineData("1234567890A")]
    [InlineData("1234567 8901")]
    public void IsValid_NonNumeric_ReturnsFalse(string tcNo)
    {
        Assert.False(_sut.IsValid(tcNo));
    }

    [Fact]
    public void RegisterViewModel_TcNo_DoesNotUseTurkishIdentityNumberAttribute()
    {
        var attributes = typeof(RegisterViewModel)
            .GetProperty(nameof(RegisterViewModel.TcNo))!
            .GetCustomAttributes(typeof(TurkishIdentityNumberAttribute), inherit: true);

        Assert.Empty(attributes);
    }

    [Fact]
    public void RegisterViewModel_IdentityNumber_DoesNotUseUnconditionalTurkishIdentityAttribute()
    {
        // Ortak alan (T.C./YKN/Pasaport); koşulsuz attribute YKN/pasaportu kırardı.
        var attributes = typeof(RegisterViewModel)
            .GetProperty(nameof(RegisterViewModel.IdentityNumber))!
            .GetCustomAttributes(typeof(TurkishIdentityNumberAttribute), inherit: true);

        Assert.Empty(attributes);
    }

    [Theory]
    [InlineData("12345678901")]
    [InlineData("01234567890")]
    [InlineData("11111111111")]
    [InlineData("1234567890")]
    [InlineData("123456789012")]
    public void RegisterViewModel_TurkishType_RejectsInvalidIdentityNumber(string invalidTc)
    {
        var model = CreateValidRegisterModel();
        model.IdentityNumber = invalidTc;

        var results = Validate(model);

        Assert.Contains(results, r =>
            r.MemberNames.Contains(nameof(RegisterViewModel.IdentityNumber))
            && r.ErrorMessage == TurkishIdentityNumber.InvalidMessage);
        Assert.DoesNotContain(invalidTc, string.Join('|', results.Select(r => r.ErrorMessage)), StringComparison.Ordinal);
    }

    [Fact]
    public void RegisterViewModel_TurkishType_AcceptsValidSyntheticIdentityNumber()
    {
        var model = CreateValidRegisterModel();
        model.IdentityNumber = "10000000146";

        var results = Validate(model);

        Assert.DoesNotContain(results, r =>
            r.MemberNames.Contains(nameof(RegisterViewModel.IdentityNumber)));
    }

    [Fact]
    public void RegisterViewModel_ForeignType_DoesNotApplyTurkishChecksum()
    {
        var model = CreateValidRegisterModel();
        model.IdentityDocumentType = Domain.Enums.IdentityDocumentType.ForeignIdentityNumber;
        model.IdentityNumber = "99999999999";
        model.NationalityCountryCode = "DE";

        var results = Validate(model);

        Assert.DoesNotContain(results, r =>
            r.ErrorMessage == TurkishIdentityNumber.InvalidMessage);
    }

    [Fact]
    public void CreateStaffUserViewModel_RejectsAlgorithmicallyInvalidTcNo()
    {
        var model = new CreateStaffUserViewModel
        {
            TcNo = "12345678901",
            Email = "a@b.com",
            FirstName = "Ad",
            LastName = "Soyad",
            RoleId = Guid.NewGuid(),
            Password = "Password1",
            ConfirmPassword = "Password1"
        };

        var results = Validate(model);
        Assert.Contains(results, r =>
            r.MemberNames.Contains(nameof(CreateStaffUserViewModel.TcNo))
            && r.ErrorMessage == TurkishIdentityNumber.InvalidMessage);
    }

    [Fact]
    public void Attribute_DoesNotEchoRawTcNoInErrorMessage()
    {
        var attribute = new TurkishIdentityNumberAttribute();
        var context = new ValidationContext(new object()) { MemberName = "TcNo", DisplayName = "T.C. Kimlik Numarası" };
        var result = attribute.GetValidationResult("12345678901", context);

        Assert.NotNull(result);
        Assert.Equal(TurkishIdentityNumber.InvalidMessage, result!.ErrorMessage);
        Assert.DoesNotContain("12345678901", result.ErrorMessage!, StringComparison.Ordinal);
    }

    private static RegisterViewModel CreateValidRegisterModel() => new()
    {
        FirstName = "Ali",
        LastName = "Veli",
        IdentityDocumentType = Domain.Enums.IdentityDocumentType.TurkishIdentityNumber,
        IdentityNumber = "10000000146",
        NationalityCountryCode = "TR",
        BirthDate = new DateOnly(2000, 6, 15),
        Email = "ali@example.com",
        Phone = "05321234567",
        Password = "Passw0rd!",
        ConfirmPassword = "Passw0rd!",
        YgsScore = 250,
        YgsYearId = Guid.NewGuid()
    };

    private static IList<ValidationResult> Validate(object model)
    {
        var context = new ValidationContext(model);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, context, results, validateAllProperties: true);
        return results;
    }
}
