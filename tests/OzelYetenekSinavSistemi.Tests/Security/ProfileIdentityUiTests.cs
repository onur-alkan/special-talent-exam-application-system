using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Application.Services;
using OzelYetenekSinavSistemi.Application.ViewModels.Profile;
using OzelYetenekSinavSistemi.Domain.Common;
using OzelYetenekSinavSistemi.Domain.Entities;
using OzelYetenekSinavSistemi.Domain.Enums;
using OzelYetenekSinavSistemi.Tests.Services;
using OzelYetenekSinavSistemi.Web.Controllers;

namespace OzelYetenekSinavSistemi.Tests.Security;

public sealed class ProfileIdentityUiTests
{
    [Fact]
    public void ProfileMarkup_ShowsReadOnlyIdentitySectionOutsideEditableFields()
    {
        var view = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "Views", "CandidateProfile", "Index.cshtml");

        Assert.Contains("Kimlik ve Uyruk Bilgileri", view, StringComparison.Ordinal);
        Assert.Contains("Model.Identity.DocumentTypeDisplay", view, StringComparison.Ordinal);
        Assert.Contains("Model.Identity.IdentityNumber", view, StringComparison.Ordinal);
        Assert.Contains("Model.Identity.NationalityDisplay", view, StringComparison.Ordinal);
        Assert.Contains("data-oys-single-submit", view, StringComparison.Ordinal);
        Assert.DoesNotContain("asp-for=\"TcNo\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("asp-for=\"Nationality\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("IdentityDocumentType", view, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildTurkishUser_ShowsTurkishIdentityAndTurkey()
    {
        var user = new User
        {
            IdentityDocumentType = IdentityDocumentType.TurkishIdentityNumber,
            IdentityNumber = "10000000146",
            NationalityCountryCode = "TR",
            TcNo = "10000000146"
        };

        var display = ProfileIdentityDisplayBuilder.Build(user, new CountryCatalog());

        Assert.Equal("T.C. Kimlik Kartı", display.DocumentTypeDisplay);
        Assert.Equal("10000000146", display.IdentityNumber);
        Assert.Equal("Türkiye", display.NationalityDisplay);
    }

    [Fact]
    public void BuildForeignUser_ShowsCountryNameFromCatalog()
    {
        var user = new User
        {
            IdentityDocumentType = IdentityDocumentType.ForeignIdentityNumber,
            IdentityNumber = "99999999999",
            NationalityCountryCode = "DE"
        };

        var display = ProfileIdentityDisplayBuilder.Build(user, new CountryCatalog());

        Assert.Equal("Yabancı Kimlik Numarası", display.DocumentTypeDisplay);
        Assert.Equal("Almanya", display.NationalityDisplay);
    }

    [Fact]
    public void BuildPassportUser_ShowsIssuingCountryAndExpiry()
    {
        var user = new User
        {
            IdentityDocumentType = IdentityDocumentType.Passport,
            IdentityNumber = "AB1234567",
            NationalityCountryCode = "DE",
            IssuingCountryCode = "DE",
            PassportExpiryDate = new DateOnly(2030, 6, 15)
        };

        var display = ProfileIdentityDisplayBuilder.Build(user, new CountryCatalog());

        Assert.Equal("Pasaport", display.DocumentTypeDisplay);
        Assert.Equal("Almanya", display.IssuingCountryDisplay);
        Assert.Equal("15.06.2030", display.PassportExpiryDisplay);
    }

    [Fact]
    public void BuildLegacyUser_FallsBackToTcNo()
    {
        var user = new User
        {
            TcNo = "10000000146"
        };

        var display = ProfileIdentityDisplayBuilder.Build(user, new CountryCatalog());

        Assert.Equal("T.C. Kimlik Kartı", display.DocumentTypeDisplay);
        Assert.Equal("10000000146", display.IdentityNumber);
        Assert.Equal("Türkiye", display.NationalityDisplay);
    }

    [Fact]
    public async Task ProfilePost_TamperedIdentityFields_DoNotReachUpdateService()
    {
        var userId = Guid.NewGuid();
        var userService = new Mock<IUserService>();
        userService.Setup(s => s.UpdateProfileAsync(
                userId,
                It.IsAny<ProfileUpdateViewModel>(),
                It.IsAny<PhotoUploadRequest?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult.Ok());

        var years = new Mock<IYgsYearRepository>();
        years.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<YgsYear>());

        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
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

        var model = new ProfileUpdateViewModel
        {
            Id = userId,
            FirstName = "Ali",
            LastName = "Veli",
            Email = "ali@example.com",
            Phone = "05321234567"
        };

        await Assert.ThrowsAnyAsync<Exception>(() => controller.Index(model, Photo: null, CancellationToken.None));

        userService.Verify(s => s.UpdateProfileAsync(
            userId,
            It.Is<ProfileUpdateViewModel>(m =>
                m.FirstName == "Ali"
                && m.Email == "ali@example.com"),
            null,
            It.IsAny<CancellationToken>()), Times.Once);
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
