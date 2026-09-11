using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Application.Services;
using OzelYetenekSinavSistemi.Application.ViewModels.Account;
using OzelYetenekSinavSistemi.Domain.Entities;
using OzelYetenekSinavSistemi.Domain.Enums;
using OzelYetenekSinavSistemi.Tests.Services;
using OzelYetenekSinavSistemi.Web.Controllers;
using OzelYetenekSinavSistemi.Web.Infrastructure;

namespace OzelYetenekSinavSistemi.Tests.Security;

public sealed class RegisterIdentityUiTests
{
    private static readonly byte[] JpegHeader = PhotoUploadTestSupport.MinimalJpeg;
    private static readonly DateOnly PassportExpiry = new(2030, 6, 15);

    [Fact]
    public void RegisterMarkup_HasIdentityFormStructureWithoutLegacyTcField()
    {
        var register = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Web", "Views", "Account", "Register.cshtml");

        Assert.Contains("data-oys-identity-form", register, StringComparison.Ordinal);
        Assert.Contains("data-oys-single-submit", register, StringComparison.Ordinal);
        Assert.Contains("asp-for=\"IdentityDocumentType\"", register, StringComparison.Ordinal);
        Assert.Contains("T.C. Kimlik Numarası", register, StringComparison.Ordinal);
        Assert.Contains("Yabancı Kimlik Numarası", register, StringComparison.Ordinal);
        Assert.Contains("Pasaport", register, StringComparison.Ordinal);
        Assert.Contains("asp-for=\"IdentityNumber\"", register, StringComparison.Ordinal);
        Assert.Contains("data-oys-nationality-select", register, StringComparison.Ordinal);
        Assert.Contains("data-oys-issuing-select", register, StringComparison.Ordinal);
        Assert.Contains("data-oys-passport-expiry", register, StringComparison.Ordinal);
        Assert.Contains("asp-validation-for=\"IdentityNumber\"", register, StringComparison.Ordinal);
        Assert.Contains("data-oys-identity-live", register, StringComparison.Ordinal);
        Assert.Contains("aria-live=\"polite\"", register, StringComparison.Ordinal);
        Assert.Contains("data-oys-turkish-nationality-code", register, StringComparison.Ordinal);
        Assert.Contains("id=\"turkish-NationalityCountryCode\"", register, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(register, "asp-for=\"NationalityCountryCode\""));
        Assert.Contains("disabled=\"@(Model.IdentityDocumentType != IdentityDocumentType.TurkishIdentityNumber", register, StringComparison.Ordinal);
        Assert.Contains("asp-for=\"Photo\"", register, StringComparison.Ordinal);
        Assert.DoesNotContain("asp-for=\"TcNo\"", register, StringComparison.Ordinal);
        Assert.DoesNotContain("onclick=", register, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onchange=", register, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SiteJs_HasIdentityDocumentBindingAndValidation()
    {
        var siteJs = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "wwwroot", "js", "site.js");
        var identityBlock = ExtractBetween(siteJs, "function oysNormalizePassportNumber", "document.addEventListener(\"DOMContentLoaded\"");
        var refreshBlock = ExtractBetween(siteJs, "function oysRefreshIdentityValidationRules", "function oysApplyIdentityDocumentVisibility");

        Assert.Contains("function oysBindIdentityDocumentForms", siteJs, StringComparison.Ordinal);
        Assert.Contains("form[data-oys-identity-form]", siteJs, StringComparison.Ordinal);
        Assert.Contains("oysIsValidTurkishIdentityNumber", identityBlock, StringComparison.Ordinal);
        Assert.Contains("function oysIsValidForeignIdentityNumber", identityBlock, StringComparison.Ordinal);
        Assert.Contains("function oysNormalizePassportNumber", identityBlock, StringComparison.Ordinal);
        Assert.Contains("code !== \"TR\"", identityBlock, StringComparison.Ordinal);
        Assert.Contains("addEventListener(\"pageshow\"", identityBlock, StringComparison.Ordinal);
        Assert.Contains("event.persisted", identityBlock, StringComparison.Ordinal);
        Assert.Contains("oysBindIdentityDocumentForms()", siteJs, StringComparison.Ordinal);
        Assert.Contains("oysEnsureUnobtrusiveFormValidator(form)", refreshBlock, StringComparison.Ordinal);
        Assert.Contains("turkishidentity: true", refreshBlock, StringComparison.Ordinal);
        Assert.Contains("Geçerli bir T.C. Kimlik Numarası giriniz.", refreshBlock, StringComparison.Ordinal);
        Assert.Contains("foreignidentity: true", refreshBlock, StringComparison.Ordinal);
        Assert.Contains("passportidentity: true", refreshBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("setTimeout(", identityBlock, StringComparison.Ordinal);
    }

    [Fact]
    public void SiteJs_IdentityTypeChange_ClearsOnlyIdentityNumber()
    {
        var siteJs = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "wwwroot", "js", "site.js");
        var bindBlock = ExtractBetween(siteJs, "function oysBindIdentityDocumentForms", "function oysLooksLikeLoginEmail");

        Assert.Contains("oysClearIdentityNumberClientError(form, identityInput)", bindBlock, StringComparison.Ordinal);
        Assert.Contains("function oysClearIdentityNumberClientError", siteJs, StringComparison.Ordinal);
        Assert.Contains("function oysIdentityNumberRequiredMessage", siteJs, StringComparison.Ordinal);
        Assert.Contains("Pasaport numaranızı giriniz.", siteJs, StringComparison.Ordinal);
        Assert.Contains("Yabancı kimlik numaranızı giriniz.", siteJs, StringComparison.Ordinal);
        Assert.Contains("T.C. Kimlik Numaranızı giriniz.", siteJs, StringComparison.Ordinal);
        Assert.DoesNotContain("required: \"Kimlik numaranızı giriniz.\"", siteJs, StringComparison.Ordinal);
        Assert.DoesNotContain("oysClearFieldValue(form.querySelector(\"[name=\\\"FirstName\\\"]\"))", bindBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("FirstName", bindBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("LastName", bindBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("BirthDate", bindBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("data-oys-birth-date", bindBlock, StringComparison.Ordinal);
    }

    [Fact]
    public void SiteJs_RejectsKnownInvalidSyntheticTurkishIdentities()
    {
        var siteJs = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "wwwroot", "js", "site.js");
        var algorithm = ExtractBetween(siteJs, "function oysIsValidTurkishIdentityNumber", "function oysIsValidForeignIdentityNumber");

        Assert.Contains("/^[1-9][0-9]{10}$/", algorithm, StringComparison.Ordinal);
        Assert.True(TurkishIdentityNumber.IsValid("10000000146"));
        Assert.False(TurkishIdentityNumber.IsValid("12345678901"));
        Assert.False(TurkishIdentityNumber.IsValid("01234567890"));
        Assert.False(TurkishIdentityNumber.IsValid("11111111111"));
        Assert.False(TurkishIdentityNumber.IsValid("1234567890"));
        Assert.False(TurkishIdentityNumber.IsValid("123456789012"));
    }

    [Fact]
    public async Task RegisterPost_ValidTurkishIdentity_ReachesAuthenticationService()
    {
        var yearId = Guid.NewGuid();
        var auth = new Mock<IAuthenticationService>();
        auth.Setup(a => a.RegisterCandidateAsync(
                It.IsAny<RegisterViewModel>(),
                It.IsAny<PhotoUploadRequest?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<Guid>.Ok(Guid.NewGuid()));

        var controller = CreateController(auth.Object, yearId);
        var model = ValidRegister(yearId);

        var result = await controller.Register(model, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        auth.Verify(a => a.RegisterCandidateAsync(
            It.Is<RegisterViewModel>(m =>
                m.IdentityDocumentType == IdentityDocumentType.TurkishIdentityNumber
                && m.IdentityNumber == "10000000146"
                && m.NationalityCountryCode == "TR"),
            It.IsAny<PhotoUploadRequest?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterPost_InvalidTurkishIdentity_DoesNotReachAuthenticationService()
    {
        var yearId = Guid.NewGuid();
        var auth = new Mock<IAuthenticationService>(MockBehavior.Strict);
        var controller = CreateController(auth.Object, yearId);
        var model = ValidRegister(yearId);
        model.IdentityNumber = "12345678901";

        var result = await controller.Register(model, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains(
            controller.ModelState[nameof(RegisterViewModel.IdentityNumber)]!.Errors,
            e => e.ErrorMessage == TurkishIdentityNumber.InvalidMessage);
        auth.Verify(a => a.RegisterCandidateAsync(
            It.IsAny<RegisterViewModel>(),
            It.IsAny<PhotoUploadRequest?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("12345678901")]
    [InlineData("01234567890")]
    [InlineData("11111111111")]
    [InlineData("1234567890")]
    [InlineData("123456789012")]
    public async Task RegisterPost_InvalidTurkishIdentityVariants_SurfaceExactFieldMessage(string invalidTc)
    {
        var yearId = Guid.NewGuid();
        var auth = new Mock<IAuthenticationService>(MockBehavior.Strict);
        var controller = CreateController(auth.Object, yearId);
        var model = ValidRegister(yearId);
        model.IdentityNumber = invalidTc;

        var result = await controller.Register(model, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Contains(
            controller.ModelState[nameof(RegisterViewModel.IdentityNumber)]!.Errors,
            e => e.ErrorMessage == TurkishIdentityNumber.InvalidMessage);
        auth.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RegisterPost_InvalidTurkishIdentity_WithMissingPhoto_StillShowsIdentityFieldError()
    {
        var yearId = Guid.NewGuid();
        var auth = new Mock<IAuthenticationService>(MockBehavior.Strict);
        var controller = CreateController(auth.Object, yearId);
        var model = ValidRegister(yearId);
        model.IdentityNumber = "12345678901";
        model.Photo = null;

        var result = await controller.Register(model, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Contains(
            controller.ModelState[nameof(RegisterViewModel.IdentityNumber)]!.Errors,
            e => e.ErrorMessage == TurkishIdentityNumber.InvalidMessage);
        Assert.True(controller.ModelState[nameof(RegisterViewModel.Photo)]!.Errors.Count > 0);
        auth.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RegisterPost_ValidForeignIdentity_ReachesAuthenticationService()
    {
        var yearId = Guid.NewGuid();
        var auth = new Mock<IAuthenticationService>();
        auth.Setup(a => a.RegisterCandidateAsync(
                It.IsAny<RegisterViewModel>(),
                It.IsAny<PhotoUploadRequest?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<Guid>.Ok(Guid.NewGuid()));

        var controller = CreateController(auth.Object, yearId);
        var model = ValidRegister(yearId);
        model.IdentityDocumentType = IdentityDocumentType.ForeignIdentityNumber;
        model.IdentityNumber = "99999999999";
        model.NationalityCountryCode = "DE";

        var result = await controller.Register(model, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        auth.Verify(a => a.RegisterCandidateAsync(
            It.Is<RegisterViewModel>(m => m.IdentityDocumentType == IdentityDocumentType.ForeignIdentityNumber),
            It.IsAny<PhotoUploadRequest?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterPost_ForeignIdentityWithTurkishNationality_IsRejectedBeforeService()
    {
        var yearId = Guid.NewGuid();
        var auth = new Mock<IAuthenticationService>(MockBehavior.Strict);
        var controller = CreateController(auth.Object, yearId);
        var model = ValidRegister(yearId);
        model.IdentityDocumentType = IdentityDocumentType.ForeignIdentityNumber;
        model.IdentityNumber = "99999999999";
        model.NationalityCountryCode = "TR";

        var result = await controller.Register(model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains(controller.ModelState[nameof(RegisterViewModel.NationalityCountryCode)]!.Errors,
            e => e.ErrorMessage!.Contains("TR olamaz", StringComparison.Ordinal));
        auth.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RegisterPost_ValidPassport_ReachesAuthenticationService()
    {
        var yearId = Guid.NewGuid();
        var auth = new Mock<IAuthenticationService>();
        auth.Setup(a => a.RegisterCandidateAsync(
                It.IsAny<RegisterViewModel>(),
                It.IsAny<PhotoUploadRequest?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<Guid>.Ok(Guid.NewGuid()));

        var controller = CreateController(auth.Object, yearId);
        var model = ValidRegister(yearId);
        model.IdentityDocumentType = IdentityDocumentType.Passport;
        model.IdentityNumber = "AB1234567";
        model.NationalityCountryCode = "DE";
        model.IssuingCountryCode = "DE";
        model.PassportExpiryDate = PassportExpiry;

        var result = await controller.Register(model, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
    }

    [Fact]
    public async Task RegisterPost_PassportMissingIssuingCountry_IsRejectedBeforeService()
    {
        var yearId = Guid.NewGuid();
        var auth = new Mock<IAuthenticationService>(MockBehavior.Strict);
        var controller = CreateController(auth.Object, yearId);
        var model = ValidRegister(yearId);
        model.IdentityDocumentType = IdentityDocumentType.Passport;
        model.IdentityNumber = "AB1234567";
        model.NationalityCountryCode = "DE";
        model.PassportExpiryDate = PassportExpiry;

        var result = await controller.Register(model, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Contains(controller.ModelState[nameof(RegisterViewModel.IssuingCountryCode)]!.Errors, e => e.ErrorMessage!.Length > 0);
        auth.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RegisterPost_PassportPastExpiry_IsRejectedBeforeService()
    {
        var yearId = Guid.NewGuid();
        var auth = new Mock<IAuthenticationService>(MockBehavior.Strict);
        var controller = CreateController(auth.Object, yearId);
        var model = ValidRegister(yearId);
        model.IdentityDocumentType = IdentityDocumentType.Passport;
        model.IdentityNumber = "AB1234567";
        model.NationalityCountryCode = "DE";
        model.IssuingCountryCode = "DE";
        model.PassportExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));

        var result = await controller.Register(model, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Contains(controller.ModelState[nameof(RegisterViewModel.PassportExpiryDate)]!.Errors,
            e => e.ErrorMessage!.Contains("geçmiş", StringComparison.OrdinalIgnoreCase));
        auth.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RegisterPost_ValidationError_RepopulatesCountryListsAndPreservesSelection()
    {
        var yearId = Guid.NewGuid();
        var controller = CreateController(Mock.Of<IAuthenticationService>(), yearId);
        var model = ValidRegister(yearId);
        model.IdentityDocumentType = IdentityDocumentType.ForeignIdentityNumber;
        model.IdentityNumber = "99999999999";
        model.NationalityCountryCode = "DE";
        model.IdentityNumber = "89000000001";

        var result = await controller.Register(model, CancellationToken.None);
        var view = Assert.IsType<ViewResult>(result);
        var returned = Assert.IsType<RegisterViewModel>(view.Model);

        Assert.Equal(IdentityDocumentType.ForeignIdentityNumber, returned.IdentityDocumentType);
        Assert.NotEmpty(returned.ForeignNationalityCountries);
        Assert.NotEmpty(returned.IssuingCountries);
        Assert.Contains(returned.ForeignNationalityCountries, c => c.Code == "DE");
    }

    private static AccountController CreateController(IAuthenticationService auth, Guid yearId)
    {
        var ygsRepo = new Mock<IYgsYearRepository>();
        ygsRepo.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<YgsYear> { new() { Id = yearId, Year = 2024 } });
        ygsRepo.Setup(r => r.GetByIdAsync(yearId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new YgsYear { Id = yearId });

        var controller = new AccountController(
            auth,
            Mock.Of<IUserService>(),
            Mock.Of<IPasswordResetService>(),
            Mock.Of<ICaptchaService>(),
            ygsRepo.Object,
            Mock.Of<IPublicUrlBuilder>(),
            new CountryCatalog(),
            new IdentityDocumentValidator(TimeProvider.System),
            PhotoUploadTestSupport.CreateAcceptingPhotoService())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
        };

        return controller;
    }

    private static RegisterViewModel ValidRegister(Guid yearId) => new()
    {
        FirstName = "Ali",
        LastName = "Veli",
        IdentityDocumentType = IdentityDocumentType.TurkishIdentityNumber,
        IdentityNumber = "10000000146",
        NationalityCountryCode = "TR",
        BirthDate = new DateOnly(2000, 6, 15),
        Email = "ali@example.com",
        Phone = "05321234567",
        Password = "Passw0rd!",
        ConfirmPassword = "Passw0rd!",
        YgsScore = 250,
        YgsYearId = yearId,
        Photo = FormFile(PhotoUploadTestSupport.MinimalJpeg, "photo.jpg", "image/jpeg")
    };

    private static FormFile FormFile(byte[] content, string fileName, string contentType)
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, content.Length, nameof(RegisterViewModel.Photo), fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static string ExtractBetween(string source, string start, string end)
    {
        var startIndex = source.IndexOf(start, StringComparison.Ordinal);
        var endIndex = source.IndexOf(end, startIndex, StringComparison.Ordinal);
        Assert.True(startIndex >= 0 && endIndex > startIndex);
        return source.Substring(startIndex, endIndex - startIndex);
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
