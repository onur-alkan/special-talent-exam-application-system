using System.ComponentModel.DataAnnotations;
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

public sealed class DynamicIdentityRequiredMessageTests
{
    private static readonly DateOnly PassportExpiry = new(2030, 6, 15);

    [Theory]
    [InlineData(IdentityDocumentType.TurkishIdentityNumber, IdentityDocumentMessages.RequiredTurkish)]
    [InlineData(IdentityDocumentType.ForeignIdentityNumber, IdentityDocumentMessages.RequiredForeign)]
    [InlineData(IdentityDocumentType.Passport, IdentityDocumentMessages.RequiredPassport)]
    public void IdentityDocumentMessages_RequiredNumber_MatchesDocumentType(
        IdentityDocumentType type,
        string expected)
    {
        Assert.Equal(expected, IdentityDocumentMessages.RequiredNumber(type));
        Assert.NotEqual(IdentityDocumentMessages.RequiredGenericLegacy, expected);
    }

    [Theory]
    [InlineData(IdentityDocumentType.TurkishIdentityNumber, IdentityDocumentMessages.RequiredTurkish)]
    [InlineData(IdentityDocumentType.ForeignIdentityNumber, IdentityDocumentMessages.RequiredForeign)]
    [InlineData(IdentityDocumentType.Passport, IdentityDocumentMessages.RequiredPassport)]
    public void Validator_EmptyIdentityNumber_ReturnsTypeSpecificRequiredMessage(
        IdentityDocumentType type,
        string expected)
    {
        var sut = new IdentityDocumentValidator(TimeProvider.System);
        var result = sut.Validate(new IdentityDocumentValidationRequest
        {
            DocumentType = type,
            IdentityNumber = null,
            NationalityCountryCode = type == IdentityDocumentType.TurkishIdentityNumber ? "TR" : "DE",
            IssuingCountryCode = type == IdentityDocumentType.Passport ? "DE" : null,
            PassportExpiryDate = type == IdentityDocumentType.Passport ? PassportExpiry : null
        });

        Assert.False(result.IsValid);
        Assert.Equal(expected, result.ErrorMessage);
    }

    [Fact]
    public void RegisterViewModel_IdentityNumber_DoesNotUseGenericRequiredAttribute()
    {
        var attrs = typeof(RegisterViewModel)
            .GetProperty(nameof(RegisterViewModel.IdentityNumber))!
            .GetCustomAttributes(typeof(RequiredAttribute), inherit: true);

        Assert.Empty(attrs);

        var source = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Application", "ViewModels", "Account", "RegisterViewModel.cs");
        Assert.DoesNotContain(IdentityDocumentMessages.RequiredGenericLegacy, source, StringComparison.Ordinal);
    }

    [Fact]
    public void RegisterMarkup_WiresIdentityValidationAriaDescribedBy()
    {
        var register = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Web", "Views", "Account", "Register.cshtml");

        Assert.Contains("aria-describedby=\"identity-number-help identity-number-validation\"", register, StringComparison.Ordinal);
        Assert.Contains("id=\"identity-number-validation\"", register, StringComparison.Ordinal);
        Assert.Contains("asp-validation-for=\"IdentityNumber\"", register, StringComparison.Ordinal);
    }

    [Fact]
    public void SiteJs_AndServer_ShareSameRequiredMessages()
    {
        var siteJs = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "wwwroot", "js", "site.js");
        Assert.Contains(IdentityDocumentMessages.RequiredTurkish, siteJs, StringComparison.Ordinal);
        Assert.Contains(IdentityDocumentMessages.RequiredForeign, siteJs, StringComparison.Ordinal);
        Assert.Contains(IdentityDocumentMessages.RequiredPassport, siteJs, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "required: \"" + IdentityDocumentMessages.RequiredGenericLegacy + "\"",
            siteJs,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(IdentityDocumentType.TurkishIdentityNumber, IdentityDocumentMessages.RequiredTurkish)]
    [InlineData(IdentityDocumentType.ForeignIdentityNumber, IdentityDocumentMessages.RequiredForeign)]
    [InlineData(IdentityDocumentType.Passport, IdentityDocumentMessages.RequiredPassport)]
    public async Task RegisterPost_EmptyIdentity_MapsTypeSpecificMessage(
        IdentityDocumentType type,
        string expected)
    {
        var yearId = Guid.NewGuid();
        var controller = CreateController(out var userRepo, yearId);
        var model = ValidRegister(yearId);
        model.IdentityDocumentType = type;
        model.IdentityNumber = null;
        if (type == IdentityDocumentType.ForeignIdentityNumber)
        {
            model.NationalityCountryCode = "DE";
        }
        else if (type == IdentityDocumentType.Passport)
        {
            model.NationalityCountryCode = "DE";
            model.IssuingCountryCode = "DE";
            model.PassportExpiryDate = PassportExpiry;
        }
        else
        {
            model.NationalityCountryCode = "TR";
        }

        var result = await controller.Register(model, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Contains(
            controller.ModelState[nameof(RegisterViewModel.IdentityNumber)]!.Errors,
            e => e.ErrorMessage == expected);
        Assert.DoesNotContain(
            controller.ModelState[nameof(RegisterViewModel.IdentityNumber)]!.Errors,
            e => e.ErrorMessage == IdentityDocumentMessages.RequiredGenericLegacy);
        Assert.Equal(type, model.IdentityDocumentType);
        userRepo.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisterPost_EmptyPassport_KeepsMessageWhenOtherFieldsAlsoInvalid()
    {
        var yearId = Guid.NewGuid();
        var controller = CreateController(out var userRepo, yearId);
        var model = ValidRegister(yearId);
        model.IdentityDocumentType = IdentityDocumentType.Passport;
        model.IdentityNumber = " ";
        model.NationalityCountryCode = "DE";
        model.IssuingCountryCode = "DE";
        model.PassportExpiryDate = PassportExpiry;
        controller.ModelState.AddModelError(nameof(RegisterViewModel.FirstName), "Adınızı giriniz.");

        var result = await controller.Register(model, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains(
            controller.ModelState[nameof(RegisterViewModel.IdentityNumber)]!.Errors,
            e => e.ErrorMessage == IdentityDocumentMessages.RequiredPassport);
        Assert.Contains(
            controller.ModelState[nameof(RegisterViewModel.FirstName)]!.Errors,
            e => e.ErrorMessage == "Adınızı giriniz.");
        userRepo.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static AccountController CreateController(out Mock<IUserRepository> userRepo, Guid yearId)
    {
        userRepo = new Mock<IUserRepository>();
        var ygsRepo = new Mock<IYgsYearRepository>();
        ygsRepo.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<YgsYear> { new() { Id = yearId, Year = 2024 } });
        ygsRepo.Setup(r => r.GetByIdAsync(yearId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new YgsYear { Id = yearId, Year = 2024 });

        var auth = new AuthenticationService(
            userRepo.Object,
            ygsRepo.Object,
            Mock.Of<IPasswordService>(),
            new IdentityDocumentValidator(TimeProvider.System),
            PhotoUploadTestSupport.CreateAcceptingPhotoService(),
            Mock.Of<IAuditService>(),
            Mock.Of<ISensitiveDataMaskingService>(),
            PhotoUploadTestSupport.SharedLock,
            TimeProvider.System);

        return new AccountController(
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
    }

    private static RegisterViewModel ValidRegister(Guid yearId) => new()
    {
        FirstName = "Ali",
        LastName = "Veli",
        IdentityDocumentType = IdentityDocumentType.TurkishIdentityNumber,
        IdentityNumber = "10000000146",
        NationalityCountryCode = "TR",
        BirthDate = new DateOnly(2000, 6, 15),
        BirthDateDay = 15,
        BirthDateMonth = 6,
        BirthDateYear = 2000,
        Email = "ali@example.com",
        Phone = "05321234567",
        PhoneCountryCode = "TR",
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
