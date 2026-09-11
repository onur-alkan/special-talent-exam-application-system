using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Application.Services;
using OzelYetenekSinavSistemi.Application.Validation;
using OzelYetenekSinavSistemi.Application.ViewModels.Account;
using OzelYetenekSinavSistemi.Application.ViewModels.UserManagement;
using OzelYetenekSinavSistemi.Domain.Entities;
using OzelYetenekSinavSistemi.Domain.Enums;
using OzelYetenekSinavSistemi.Web.Controllers;
using OzelYetenekSinavSistemi.Web.Infrastructure;
using OzelYetenekSinavSistemi.Tests.Services;

namespace OzelYetenekSinavSistemi.Tests.Validation;

public sealed class CandidateRegistrationValidationTests
{
    private static readonly byte[] JpegHeader = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46];

    [Theory]
    [InlineData("--------")]
    [InlineData(".......")]
    [InlineData("!!!!")]
    [InlineData("123456")]
    [InlineData("😀😀")]
    public void RegisterViewModel_InvalidFirstName_FailsValidation(string firstName)
    {
        var model = ValidRegister();
        model.FirstName = firstName;

        var results = Validate(model);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(RegisterViewModel.FirstName)));
    }

    [Fact]
    public void RegisterViewModel_WhitespaceOnlyFirstName_FailsValidation()
    {
        var model = ValidRegister();
        model.FirstName = "   ";

        var results = Validate(model);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(RegisterViewModel.FirstName)));
    }

    [Theory]
    [InlineData("--------")]
    [InlineData("........")]
    public void RegisterViewModel_InvalidLastName_FailsValidation(string lastName)
    {
        var model = ValidRegister();
        model.LastName = lastName;

        var results = Validate(model);

        Assert.Contains(
            results,
            r => r.MemberNames.Contains(nameof(RegisterViewModel.LastName))
                 && r.ErrorMessage == InputTextRules.FormatHumanNameMessage("Soyad"));
    }

    [Theory]
    [InlineData("Jean-Pierre")]
    [InlineData("Ömer Faruk")]
    [InlineData("D'Angelo")]
    [InlineData("Łukasz")]
    public void RegisterViewModel_ValidUnicodeNames_PassNameValidation(string firstName)
    {
        var model = ValidRegister();
        model.FirstName = firstName;

        Assert.DoesNotContain(
            Validate(model),
            r => r.MemberNames.Contains(nameof(RegisterViewModel.FirstName)));
    }

    [Fact]
    public async Task RegisterPost_InvalidFirstName_DoesNotReachAuthenticationService()
    {
        var yearId = Guid.NewGuid();
        var auth = new Mock<IAuthenticationService>(MockBehavior.Strict);
        var controller = CreateController(auth.Object, yearId);
        var model = ValidRegister(yearId);
        model.FirstName = "--------";

        ApplyViewModelValidation(controller, model);
        var result = await controller.Register(model, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains(
            controller.ModelState[nameof(RegisterViewModel.FirstName)]!.Errors,
            e => e.ErrorMessage == InputTextRules.FormatHumanNameMessage("Ad"));

        auth.VerifyNoOtherCalls();
    }

    [Fact]
    public void CreateStaffUserViewModel_InvalidFirstName_FailsValidation()
    {
        var model = new CreateStaffUserViewModel
        {
            TcNo = "10000000146",
            Email = "admin@example.com",
            FirstName = "--------",
            LastName = "Yönetici",
            RoleId = Guid.NewGuid(),
            Password = "Passw0rd!",
            ConfirmPassword = "Passw0rd!"
        };

        var results = Validate(model);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateStaffUserViewModel.FirstName)));
    }

    private static IList<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, true);
        return results;
    }

    private static void ApplyViewModelValidation(Controller controller, object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, true);
        foreach (var result in results)
        {
            var members = result.MemberNames.Any() ? result.MemberNames : new[] { string.Empty };
            foreach (var member in members)
                controller.ModelState.AddModelError(member, result.ErrorMessage ?? "Geçersiz değer.");
        }
    }

    private static RegisterViewModel ValidRegister(Guid? yearId = null) => new()
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
        YgsYearId = yearId ?? Guid.NewGuid(),
        Photo = FormFile(JpegHeader, "photo.jpg", "image/jpeg")
    };

    private static AccountController CreateController(IAuthenticationService auth, Guid yearId)
    {
        var ygsRepo = new Mock<IYgsYearRepository>();
        ygsRepo.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<YgsYear> { new() { Id = yearId, Year = 2024 } });
        ygsRepo.Setup(r => r.GetByIdAsync(yearId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new YgsYear { Id = yearId });

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

    private static FormFile FormFile(byte[] content, string fileName, string contentType)
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, content.Length, nameof(RegisterViewModel.Photo), fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }
}
