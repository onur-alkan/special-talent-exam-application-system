using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Application.ViewModels.Account;
using OzelYetenekSinavSistemi.Application.ViewModels.Profile;
using OzelYetenekSinavSistemi.Domain.Common;
using OzelYetenekSinavSistemi.Domain.Entities;
using OzelYetenekSinavSistemi.Tests.Services;
using OzelYetenekSinavSistemi.Web.Controllers;
using OzelYetenekSinavSistemi.Web.Infrastructure;
using PhoneNumbers;

namespace OzelYetenekSinavSistemi.Tests.Security;

public sealed class PhoneFieldProductionTests
{
    [Fact]
    public async Task RegisterGet_ReturnsDefaultTurkeyPhoneCountry()
    {
        var years = new Mock<IYgsYearRepository>();
        years.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<YgsYear>());

        var controller = CreateAccountController(years.Object);
        var result = Assert.IsType<ViewResult>(await controller.Register(CancellationToken.None));
        var model = Assert.IsType<RegisterViewModel>(result.Model);

        Assert.Empty(model.Phone);
        Assert.Equal(MobilePhoneNumber.DefaultRegionCode, model.PhoneCountryCode);
        Assert.Contains(model.PhoneCountries, c => c.Code == "TR" && c.DialCode == 90);
        Assert.Equal("TR", model.PhoneCountries[0].Code);
        Assert.Empty(ValidateProperty(model, nameof(RegisterViewModel.PhoneCountryCode)));
    }

    [Fact]
    public void PhoneCountryCode_DoesNotUseStringLengthMinimum_ThatBreaksSelectClientValidation()
    {
        // jQuery Validate select'te getLength = seçili option sayısı; MinLength=2 TR seçiliyken bile hata üretir.
        var property = typeof(RegisterViewModel).GetProperty(nameof(RegisterViewModel.PhoneCountryCode))!;
        Assert.Empty(property.GetCustomAttributes(typeof(StringLengthAttribute), inherit: false));
        Assert.NotEmpty(property.GetCustomAttributes(typeof(RequiredAttribute), inherit: false));
        Assert.NotEmpty(property.GetCustomAttributes(typeof(RegularExpressionAttribute), inherit: false));
    }

    [Fact]
    public void PhoneCountryCode_Empty_ReturnsTurkishRequiredMessage()
    {
        var model = new RegisterViewModel { PhoneCountryCode = "" };
        var errors = ValidateProperty(model, nameof(RegisterViewModel.PhoneCountryCode));
        Assert.Contains(errors, e => e.ErrorMessage == MobilePhoneNumber.CountryRequiredMessage);
    }

    [Theory]
    [InlineData("TR")]
    [InlineData("DE")]
    [InlineData("US")]
    public void PhoneCountryCode_ValidIsoCode_PassesPropertyValidation(string code)
    {
        var model = new RegisterViewModel { PhoneCountryCode = code };
        Assert.Empty(ValidateProperty(model, nameof(RegisterViewModel.PhoneCountryCode)));
    }

    [Fact]
    public void SiteJs_RevalidatesPhoneCountrySelectOnChange()
    {
        var siteJs = ReadRuntimeFile("wwwroot", "js", "site.js");
        Assert.Contains("data-oys-phone-country", siteJs, StringComparison.Ordinal);
        Assert.Contains("$country.valid()", siteJs, StringComparison.Ordinal);
        Assert.Contains("oysEnsureUnobtrusiveFormValidator", siteJs, StringComparison.Ordinal);
    }

    [Fact]
    public void RegisterPhoneInput_UsesInternationalTelephoneAttributes()
    {
        var register = ReadRuntimeFile("Views", "Account", "Register.cshtml");

        Assert.Contains("asp-for=\"Phone\"", register, StringComparison.Ordinal);
        Assert.Contains("label asp-for=\"Phone\"", register, StringComparison.Ordinal);
        Assert.Contains("asp-for=\"PhoneCountryCode\"", register, StringComparison.Ordinal);
        Assert.Contains("type=\"tel\"", register, StringComparison.Ordinal);
        Assert.Contains("inputmode=\"tel\"", register, StringComparison.Ordinal);
        Assert.Contains("autocomplete=\"tel-national\"", register, StringComparison.Ordinal);
        Assert.Contains("autocomplete=\"tel-country-code\"", register, StringComparison.Ordinal);
        Assert.Contains("placeholder=\"5XX XXX XX XX\"", register, StringComparison.Ordinal);
        Assert.Contains("data-oys-phone-country", register, StringComparison.Ordinal);
        Assert.Contains("data-oys-phone-input", register, StringComparison.Ordinal);
        Assert.Contains("Türkiye numaraları için 5XX XXX XX XX biçiminde giriniz.", register, StringComparison.Ordinal);
        Assert.DoesNotContain("value=\"5321234567\"", register, StringComparison.Ordinal);
        Assert.DoesNotContain("inputmode=\"numeric\"", register, StringComparison.Ordinal);
    }

    [Fact]
    public void RegisterDisplayName_IsCepTelefonu()
    {
        var display = typeof(RegisterViewModel)
            .GetProperty(nameof(RegisterViewModel.Phone))!
            .GetCustomAttributes(typeof(DisplayAttribute), false)
            .Cast<DisplayAttribute>()
            .Single();

        Assert.Equal("Cep Telefonu", display.Name);
    }

    [Fact]
    public void RuntimePhoneSources_DoNotContainFixedExampleNumber()
    {
        var runtimeRoot = FindUnder("src");
        var files = Directory.EnumerateFiles(runtimeRoot, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase));

        foreach (var file in files)
        {
            Assert.DoesNotContain("5321234567", File.ReadAllText(file), StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("5551234567", "TR", "+905551234567")]
    [InlineData("05551234567", "TR", "+905551234567")]
    [InlineData("+905551234567", "TR", "+905551234567")]
    [InlineData("0555 123 45 67", "TR", "+905551234567")]
    [InlineData("555-123-45-67", "TR", "+905551234567")]
    [InlineData("(555) 123 45 67", "TR", "+905551234567")]
    public void TurkishPhoneInputs_NormalizeToE164(string input, string country, string expectedE164)
    {
        Assert.True(MobilePhoneNumber.TryValidateAndNormalize(country, input, out var e164, out var error));
        Assert.Null(error);
        Assert.Equal(expectedE164, e164);

        var model = new RegisterViewModel { PhoneCountryCode = country, Phone = input };
        Assert.DoesNotContain(
            ValidateProperty(model, nameof(RegisterViewModel.Phone)),
            result => result.MemberNames.Contains(nameof(RegisterViewModel.Phone)));
    }

    [Fact]
    public void EmptyPhone_ReturnsRequiredTurkishMessage()
    {
        var model = new RegisterViewModel { PhoneCountryCode = "TR", Phone = "" };
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);

        Assert.Contains(results, r =>
            r.MemberNames.Contains(nameof(RegisterViewModel.Phone))
            && r.ErrorMessage == MobilePhoneNumber.RequiredMessage);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("555123456789012345")]
    [InlineData("abcdefgijk")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("2121234567")]
    public void InvalidPhoneInputs_AreRejected(string phone)
    {
        var model = new RegisterViewModel { PhoneCountryCode = "TR", Phone = phone };
        var errors = ValidateProperty(model, nameof(RegisterViewModel.Phone));

        Assert.Contains(errors, result =>
            result.ErrorMessage == MobilePhoneNumber.InvalidMessage
            || result.ErrorMessage == MobilePhoneNumber.CountryMismatchMessage);
    }

    [Fact]
    public void MismatchedCountry_IsRejected()
    {
        var model = new RegisterViewModel { PhoneCountryCode = "US", Phone = "+905551234567" };
        var errors = ValidateProperty(model, nameof(RegisterViewModel.Phone));

        Assert.Contains(errors, result => result.ErrorMessage == MobilePhoneNumber.CountryMismatchMessage);
    }

    [Fact]
    public void LibraryExampleForeignMobile_IsAcceptedAndStoredAsE164()
    {
        var util = PhoneNumberUtil.GetInstance();
        var example = util.GetExampleNumberForType("DE", PhoneNumberType.MOBILE);
        Assert.NotNull(example);

        var national = util.Format(example, PhoneNumberFormat.NATIONAL);
        var expectedE164 = util.Format(example, PhoneNumberFormat.E164);

        Assert.True(MobilePhoneNumber.TryValidateAndNormalize("DE", national, out var e164, out var error));
        Assert.Null(error);
        Assert.Equal(expectedE164, e164);

        var model = new RegisterViewModel { PhoneCountryCode = "DE", Phone = national };
        Assert.Empty(ValidateProperty(model, nameof(RegisterViewModel.Phone)));
    }

    [Fact]
    public void ManipulatedHiddenE164_IsIgnored_VisibleInputIsReparsed()
    {
        // Gizli E.164 alanına güvenilmez; sunucu görünür girdiyi yeniden ayrıştırır.
        Assert.True(MobilePhoneNumber.TryValidateAndNormalize(
            "TR",
            "0555 123 45 67",
            out var e164,
            out _));
        Assert.Equal("+905551234567", e164);
        Assert.NotEqual("+901111111111", e164);
    }

    [Fact]
    public void PhoneMask_DoesNotRevealFullNumber()
    {
        var masked = MobilePhoneNumber.Mask("+905551234567");
        Assert.DoesNotContain("555123", masked, StringComparison.Ordinal);
        Assert.StartsWith("+90", masked, StringComparison.Ordinal);
        Assert.EndsWith("4567", masked, StringComparison.Ordinal);
        Assert.Contains('*', masked);
    }

    [Fact]
    public void OtherFieldErrors_DoNotSuppressPhoneValidation()
    {
        var years = new Mock<IYgsYearRepository>();
        years.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<YgsYear>());

        var controller = CreateAccountController(years.Object);
        var model = new RegisterViewModel
        {
            FirstName = "",
            PhoneCountryCode = "TR",
            Phone = "123"
        };

        // MVC ModelState gibi: her alan bağımsız doğrulanır.
        foreach (var property in new[]
                 {
                     nameof(RegisterViewModel.FirstName),
                     nameof(RegisterViewModel.Phone)
                 })
        {
            foreach (var error in ValidateProperty(model, property))
                controller.ModelState.AddModelError(property, error.ErrorMessage ?? "Geçersiz");
        }

        Assert.True(controller.ModelState.ContainsKey(nameof(RegisterViewModel.FirstName)));
        Assert.True(controller.ModelState.ContainsKey(nameof(RegisterViewModel.Phone)));
        Assert.Contains(
            controller.ModelState[nameof(RegisterViewModel.Phone)]!.Errors,
            e => e.ErrorMessage == MobilePhoneNumber.InvalidMessage);
    }

    [Fact]
    public async Task InvalidRegisterPost_PreservesPhoneAndCountry()
    {
        var years = new Mock<IYgsYearRepository>();
        var yearId = Guid.NewGuid();
        years.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<YgsYear> { new() { Id = yearId, Year = 2024 } });

        var controller = CreateAccountController(years.Object);
        var model = new RegisterViewModel
        {
            FirstName = "Ali",
            LastName = "Veli",
            IdentityDocumentType = Domain.Enums.IdentityDocumentType.TurkishIdentityNumber,
            IdentityNumber = "10000000146",
            NationalityCountryCode = "TR",
            BirthDate = new DateOnly(2000, 1, 1),
            Email = "not-an-email",
            PhoneCountryCode = "DE",
            Phone = "01512 3456789",
            Password = "Passw0rd!",
            ConfirmPassword = "Passw0rd!",
            YgsScore = 250,
            YgsYearId = yearId
        };

        var result = Assert.IsType<ViewResult>(await controller.Register(model, CancellationToken.None));
        var returned = Assert.IsType<RegisterViewModel>(result.Model);

        Assert.Equal("DE", returned.PhoneCountryCode);
        Assert.Equal("01512 3456789", returned.Phone);
        Assert.NotEmpty(returned.PhoneCountries);
    }

    [Fact]
    public async Task CandidateProfileGet_UsesAuthenticatedUsersPhoneOnly()
    {
        var currentUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var userService = new Mock<IUserService>(MockBehavior.Strict);
        var years = new Mock<IYgsYearRepository>();
        var ownProfile = new ProfileViewModel
        {
            Id = currentUserId,
            FirstName = "Ali",
            LastName = "Veli",
            Email = "ali@example.com",
            Phone = "05321234567"
        };

        userService.Setup(s => s.GetProfileAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<ProfileViewModel>.Ok(ownProfile));
        years.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<YgsYear>());

        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, currentUserId.ToString()),
                    new Claim(ClaimTypes.Role, DomainConstants.RoleNames.Candidate)
                },
                "Test"))
        };
        var controller = new CandidateProfileController(
            userService.Object,
            years.Object,
            PhotoUploadTestSupport.CreateAcceptingPhotoService())
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };

        var result = Assert.IsType<ViewResult>(await controller.Index(CancellationToken.None));
        var model = Assert.IsType<ProfileViewModel>(result.Model);

        Assert.Equal("05321234567", model.Phone);
        userService.Verify(s => s.GetProfileAsync(currentUserId, It.IsAny<CancellationToken>()), Times.Once);
        userService.Verify(s => s.GetProfileAsync(otherUserId, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void UsersPhoneColumn_SupportsE164Length()
    {
        var sql = File.ReadAllText(FindUnder("database", "OzelYetenekSinavSistemi.sql"));
        Assert.Contains("Phone             NVARCHAR(20)", sql, StringComparison.Ordinal);
        Assert.True(MobilePhoneNumber.MaxE164Length <= 20);
    }

    private static AccountController CreateAccountController(IYgsYearRepository years) =>
        new(
            Mock.Of<IAuthenticationService>(),
            Mock.Of<IUserService>(),
            Mock.Of<IPasswordResetService>(),
            Mock.Of<ICaptchaService>(),
            years,
            Mock.Of<IPublicUrlBuilder>(),
            new OzelYetenekSinavSistemi.Application.Services.CountryCatalog(),
            new OzelYetenekSinavSistemi.Application.Services.IdentityDocumentValidator(TimeProvider.System),
            OzelYetenekSinavSistemi.Tests.Services.PhotoUploadTestSupport.CreateAcceptingPhotoService())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

    private static IReadOnlyList<ValidationResult> ValidateProperty(object model, string propertyName)
    {
        var property = model.GetType().GetProperty(propertyName)!;
        var context = new ValidationContext(model) { MemberName = propertyName };
        var results = new List<ValidationResult>();
        Validator.TryValidateProperty(property.GetValue(model), context, results);
        return results;
    }

    private static string ReadRuntimeFile(params string[] parts)
        => File.ReadAllText(FindUnder(new[] { "src", "OzelYetenekSinavSistemi.Web" }.Concat(parts).ToArray()));

    private static string FindUnder(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate) || Directory.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        throw new FileNotFoundException(string.Join(Path.DirectorySeparatorChar, parts));
    }
}
