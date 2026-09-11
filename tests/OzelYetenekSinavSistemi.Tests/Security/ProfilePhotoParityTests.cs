using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Application.ViewModels.Profile;
using OzelYetenekSinavSistemi.Domain.Common;
using OzelYetenekSinavSistemi.Domain.Entities;
using OzelYetenekSinavSistemi.Tests.Services;
using OzelYetenekSinavSistemi.Web.Controllers;

namespace OzelYetenekSinavSistemi.Tests.Security;

public sealed class ProfilePhotoParityTests
{
    [Fact]
    public void ProfileView_UsesProfessionalPhotoComponentMatchingRegister()
    {
        var profile = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "Views", "CandidateProfile", "Index.cshtml");
        var register = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "Views", "Account", "Register.cshtml");

        Assert.Contains("data-oys-photo-upload", profile, StringComparison.Ordinal);
        Assert.Contains("data-oys-photo-input", profile, StringComparison.Ordinal);
        Assert.Contains("data-oys-photo-pick", profile, StringComparison.Ordinal);
        Assert.Contains("data-oys-photo-status", profile, StringComparison.Ordinal);
        Assert.Contains("data-oys-photo-preview-wrap", profile, StringComparison.Ordinal);
        Assert.Contains("data-oys-photo-preview", profile, StringComparison.Ordinal);
        Assert.Contains("data-oys-photo-preview-loading", profile, StringComparison.Ordinal);
        Assert.Contains("data-oys-photo-validation", profile, StringComparison.Ordinal);
        Assert.Contains("Fotoğraf Seç", profile, StringComparison.Ordinal);
        Assert.Contains("Fotoğraf seçilmedi", profile, StringComparison.Ordinal);
        Assert.Contains(">Vesikalık Fotoğraf<", profile, StringComparison.Ordinal);
        Assert.Contains("PhotoUploadMessages.ProfileKeepHint", profile, StringComparison.Ordinal);
        Assert.Contains("PhotoUploadMessages.ProfileReselectHint", profile, StringComparison.Ordinal);
        Assert.Contains("Yeni Fotoğraf Önizlemesi", profile, StringComparison.Ordinal);
        Assert.Contains("PhotoUploadLimits.AcceptAttribute", profile, StringComparison.Ordinal);
        Assert.Contains("PhotoUploadLimits.MaxFileBytes", profile, StringComparison.Ordinal);
        Assert.Contains("PhotoUploadLimits.MaxFileSizeDisplay", profile, StringComparison.Ordinal);
        Assert.Contains("Son 6 ay içinde çekilmiş", profile, StringComparison.Ordinal);
        Assert.Contains("name=\"Photo\"", profile, StringComparison.Ordinal);
        Assert.Contains("object-fit: contain", profile, StringComparison.Ordinal);
        Assert.Contains("PhotoCacheVersion", profile, StringComparison.Ordinal);
        Assert.DoesNotContain("form-control-file", profile, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"photo\"", profile, StringComparison.Ordinal);
        Assert.DoesNotContain("Dosya Seç", profile, StringComparison.Ordinal);
        Assert.DoesNotContain("Dosya seçilmedi", profile, StringComparison.Ordinal);
        Assert.DoesNotContain("PhotoUploadMessages.Required", profile, StringComparison.Ordinal);
        Assert.DoesNotContain("onchange=", profile, StringComparison.OrdinalIgnoreCase);

        var photoBlockStart = profile.IndexOf("data-oys-photo-upload", StringComparison.Ordinal);
        var photoBlockEnd = profile.IndexOf("data-oys-submit-button", photoBlockStart, StringComparison.Ordinal);
        Assert.True(photoBlockStart >= 0 && photoBlockEnd > photoBlockStart);
        var photoBlock = profile.Substring(photoBlockStart, photoBlockEnd - photoBlockStart);
        Assert.DoesNotContain("data-val-required", photoBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("required=", photoBlock, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("data-oys-photo-upload", register, StringComparison.Ordinal);
        Assert.Contains("PhotoUploadLimits.MaxFileBytes", register, StringComparison.Ordinal);
        Assert.Contains("PhotoUploadMessages.ReselectHint", register, StringComparison.Ordinal);
    }

    [Fact]
    public void SiteJs_PhotoFeedback_BindsPhotoFieldCaseInsensitively()
    {
        var js = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "wwwroot", "js", "site.js");
        var photoFnStart = js.IndexOf("function oysBindPhotoFileFeedback", StringComparison.Ordinal);
        var nextFn = js.IndexOf("\nfunction oysParseBoolParam", photoFnStart, StringComparison.Ordinal);
        var photoFn = js.Substring(photoFnStart, nextFn - photoFnStart);

        Assert.Contains("toLowerCase()", photoFn, StringComparison.Ordinal);
        Assert.Contains("fieldName !== \"photo\"", photoFn, StringComparison.Ordinal);
        Assert.DoesNotContain("input.name !== \"Photo\"", photoFn, StringComparison.Ordinal);
        Assert.Contains("readAsDataURL", photoFn, StringComparison.Ordinal);
        Assert.DoesNotContain("URL.createObjectURL", photoFn, StringComparison.Ordinal);
    }

    [Fact]
    public void ProfileViewModel_PhotoCacheVersion_UsesStoredFileNameOnly()
    {
        var guid = Guid.NewGuid().ToString("N");
        var model = new ProfileViewModel { PhotoPath = $"/uploads/photos/{guid}.jpg" };

        Assert.Equal(guid, model.PhotoCacheVersion);
        Assert.Null(new ProfileViewModel().PhotoCacheVersion);
    }

    [Fact]
    public async Task ProfilePost_WithoutPhoto_SucceedsAndDoesNotCallValidateOrSave()
    {
        var userId = Guid.NewGuid();
        var userService = new Mock<IUserService>();
        var photos = new Mock<IPhotoUploadService>(MockBehavior.Strict);
        userService.Setup(s => s.UpdateProfileAsync(
                userId,
                It.IsAny<ProfileUpdateViewModel>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult.Ok());

        var controller = CreateController(userId, userService.Object, photos.Object);
        var result = await controller.Index(ValidProfile(userId), Photo: null, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(PhotoUploadMessages.ProfileUpdatedGeneral, controller.TempData["ToastSuccess"]);
        userService.Verify(s => s.UpdateProfileAsync(userId, It.IsAny<ProfileUpdateViewModel>(), null, It.IsAny<CancellationToken>()), Times.Once);
        photos.Verify(p => p.ValidatePhotoAsync(It.IsAny<PhotoUploadRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        photos.Verify(p => p.SavePhotoAsync(It.IsAny<PhotoUploadRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProfilePost_ValidJpeg_ValidatesThenUpdatesWithPhotoRequest()
    {
        var userId = Guid.NewGuid();
        var userService = new Mock<IUserService>();
        var photos = new Mock<IPhotoUploadService>();
        photos.Setup(p => p.ValidatePhotoAsync(It.IsAny<PhotoUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PhotoUploadResult.Ok(string.Empty));
        userService.Setup(s => s.UpdateProfileAsync(
                userId,
                It.IsAny<ProfileUpdateViewModel>(),
                It.IsNotNull<PhotoUploadRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult.Ok());

        var controller = CreateController(userId, userService.Object, photos.Object);
        var result = await controller.Index(
            ValidProfile(userId),
            FormFile(PhotoUploadTestSupport.MinimalJpeg, "aday.jpg", "image/jpeg"),
            CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(PhotoUploadMessages.ProfileUpdated, controller.TempData["ToastSuccess"]);
        photos.Verify(p => p.ValidatePhotoAsync(It.IsAny<PhotoUploadRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        userService.Verify(s => s.UpdateProfileAsync(
            userId,
            It.IsAny<ProfileUpdateViewModel>(),
            It.Is<PhotoUploadRequest>(r => r.FileName == "aday.jpg" && r.Length == PhotoUploadTestSupport.MinimalJpeg.Length),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProfilePost_ValidPng_Accepted()
    {
        var userId = Guid.NewGuid();
        var userService = new Mock<IUserService>();
        var photos = new Mock<IPhotoUploadService>();
        photos.Setup(p => p.ValidatePhotoAsync(It.IsAny<PhotoUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PhotoUploadResult.Ok(string.Empty));
        userService.Setup(s => s.UpdateProfileAsync(
                userId,
                It.IsAny<ProfileUpdateViewModel>(),
                It.IsNotNull<PhotoUploadRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult.Ok());

        var controller = CreateController(userId, userService.Object, photos.Object);
        var result = await controller.Index(
            ValidProfile(userId),
            FormFile(PhotoUploadTestSupport.MinimalPng, "aday.png", "image/png"),
            CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(PhotoUploadMessages.ProfileUpdated, controller.TempData["ToastSuccess"]);
    }

    [Fact]
    public async Task ProfilePost_TxtPhoto_RejectedEvenWhenOtherFieldsInvalid_AndUpdateNotCalled()
    {
        var userId = Guid.NewGuid();
        var userService = new Mock<IUserService>();
        userService.Setup(s => s.GetProfileAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<ProfileViewModel>.Ok(new ProfileViewModel
            {
                Id = userId,
                FirstName = "Ali",
                LastName = "Veli",
                Email = "ali@example.com",
                PhotoPath = $"/uploads/photos/{Guid.NewGuid():N}.jpg"
            }));

        var photos = new Mock<IPhotoUploadService>();
        photos.Setup(p => p.ValidatePhotoAsync(It.IsAny<PhotoUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PhotoUploadResult.Fail(PhotoUploadErrorCode.UnsupportedExtension, PhotoUploadMessages.UnsupportedType));

        var controller = CreateController(userId, userService.Object, photos.Object);
        var model = ValidProfile(userId);
        model.FirstName = "";
        controller.ModelState.AddModelError(nameof(ProfileUpdateViewModel.FirstName), "Adınızı giriniz.");

        var result = await controller.Index(
            model,
            FormFile(System.Text.Encoding.UTF8.GetBytes("plain"), "notes.txt", "text/plain"),
            CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Contains(
            controller.ModelState["Photo"]!.Errors,
            e => e.ErrorMessage == PhotoUploadMessages.UnsupportedType);
        userService.Verify(s => s.UpdateProfileAsync(
            It.IsAny<Guid>(),
            It.IsAny<ProfileUpdateViewModel>(),
            It.IsAny<PhotoUploadRequest?>(),
            It.IsAny<CancellationToken>()), Times.Never);
        photos.Verify(p => p.SavePhotoAsync(It.IsAny<PhotoUploadRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProfilePost_FakeJpg_RejectedWithoutTouchingUpdate()
    {
        var userId = Guid.NewGuid();
        var oldPath = $"/uploads/photos/{Guid.NewGuid():N}.jpg";
        var userService = new Mock<IUserService>();
        userService.Setup(s => s.GetProfileAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<ProfileViewModel>.Ok(new ProfileViewModel
            {
                Id = userId,
                FirstName = "Ali",
                LastName = "Veli",
                Email = "ali@example.com",
                PhotoPath = oldPath
            }));

        var photos = new Mock<IPhotoUploadService>();
        photos.Setup(p => p.ValidatePhotoAsync(It.IsAny<PhotoUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PhotoUploadResult.Fail(PhotoUploadErrorCode.InvalidSignature, PhotoUploadMessages.UnsupportedType));

        var controller = CreateController(userId, userService.Object, photos.Object);
        var result = await controller.Index(
            ValidProfile(userId),
            FormFile(System.Text.Encoding.UTF8.GetBytes("sahte"), "sahte.jpg", "image/jpeg"),
            CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var returned = Assert.IsType<ProfileViewModel>(view.Model);
        Assert.Equal(oldPath, returned.PhotoPath);
        Assert.Contains(
            controller.ModelState["Photo"]!.Errors,
            e => e.ErrorMessage == PhotoUploadMessages.UnsupportedType);
        userService.Verify(s => s.UpdateProfileAsync(
            It.IsAny<Guid>(),
            It.IsAny<ProfileUpdateViewModel>(),
            It.IsAny<PhotoUploadRequest?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProfilePost_OversizedPhoto_Rejected()
    {
        var userId = Guid.NewGuid();
        var userService = new Mock<IUserService>();
        userService.Setup(s => s.GetProfileAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<ProfileViewModel>.Ok(new ProfileViewModel
            {
                Id = userId,
                FirstName = "Ali",
                LastName = "Veli",
                Email = "ali@example.com"
            }));

        var photos = new Mock<IPhotoUploadService>();
        photos.Setup(p => p.ValidatePhotoAsync(It.IsAny<PhotoUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PhotoUploadResult.Fail(PhotoUploadErrorCode.TooLarge, PhotoUploadMessages.TooLarge));

        var oversized = PhotoUploadTestSupport.CreateJpegOfLength((int)PhotoUploadLimits.MaxFileBytes + 1);
        var controller = CreateController(userId, userService.Object, photos.Object);
        var result = await controller.Index(
            ValidProfile(userId),
            FormFile(oversized, "big.jpg", "image/jpeg"),
            CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Contains(
            controller.ModelState["Photo"]!.Errors,
            e => e.ErrorMessage == PhotoUploadMessages.TooLarge);
        userService.Verify(s => s.UpdateProfileAsync(
            It.IsAny<Guid>(),
            It.IsAny<ProfileUpdateViewModel>(),
            It.IsAny<PhotoUploadRequest?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProfilePost_DoesNotTrustClientPhotoPathHiddenField()
    {
        var profileSource = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "Views", "CandidateProfile", "Index.cshtml");
        Assert.DoesNotContain("name=\"PhotoPath\"", profileSource, StringComparison.Ordinal);
        Assert.DoesNotContain("asp-for=\"PhotoPath\"", profileSource, StringComparison.Ordinal);

        var userId = Guid.NewGuid();
        var userService = new Mock<IUserService>();
        userService.Setup(s => s.UpdateProfileAsync(
                userId,
                It.IsAny<ProfileUpdateViewModel>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult.Ok());

        var controller = CreateController(userId, userService.Object, PhotoUploadTestSupport.CreateAcceptingPhotoService());
        await controller.Index(ValidProfile(userId), Photo: null, CancellationToken.None);

        userService.Verify(s => s.UpdateProfileAsync(
            userId,
            It.IsAny<ProfileUpdateViewModel>(),
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ProfileController_UsesSharedPhotoValidationService()
    {
        var source = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "Controllers", "CandidateProfileController.cs");
        Assert.Contains("ValidatePhotoAsync", source, StringComparison.Ordinal);
        Assert.Contains("IPhotoUploadService", source, StringComparison.Ordinal);
        Assert.Contains("HasUploadedPhoto", source, StringComparison.Ordinal);
        Assert.Contains("PhotoUploadMessages.ProfileUpdated", source, StringComparison.Ordinal);
        Assert.DoesNotContain("PhotoUploadMessages.Required", source, StringComparison.Ordinal);
    }

    private static CandidateProfileController CreateController(
        Guid userId,
        IUserService userService,
        IPhotoUploadService photos)
    {
        var years = new Mock<IYgsYearRepository>();
        years.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<YgsYear>());

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                    new Claim(ClaimTypes.Role, DomainConstants.RoleNames.Candidate)
                },
                "Test"))
        };

        return new CandidateProfileController(userService, years.Object, photos)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>())
        };
    }

    private static ProfileUpdateViewModel ValidProfile(Guid userId) => new()
    {
        Id = userId,
        FirstName = "Ali",
        LastName = "Veli",
        Email = "ali@example.com",
        Phone = "05321234567"
    };

    private static FormFile FormFile(byte[] content, string fileName, string contentType)
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, content.Length, "Photo", fileName)
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
