using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Options;
using Moq;
using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Application.Services;
using OzelYetenekSinavSistemi.Application.ViewModels.Account;
using OzelYetenekSinavSistemi.Domain.Entities;
using OzelYetenekSinavSistemi.Infrastructure.Configuration;
using OzelYetenekSinavSistemi.Infrastructure.Services;
using OzelYetenekSinavSistemi.Tests.Services;
using OzelYetenekSinavSistemi.Web.Controllers;
using OzelYetenekSinavSistemi.Web.Infrastructure;

namespace OzelYetenekSinavSistemi.Tests.Security;

public sealed class RegisterPhotoValidationTests : IDisposable
{
    private readonly string _storageRoot;
    private readonly PhotoUploadService _photos;

    public RegisterPhotoValidationTests()
    {
        _storageRoot = Path.Combine(Path.GetTempPath(), "oys-photo-msg-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_storageRoot);
        _photos = new PhotoUploadService(Options.Create(new PhotoUploadOptions
        {
            StorageRootPath = _storageRoot,
            MaxBytes = PhotoUploadLimits.MaxFileBytes
        }));
    }

    [Fact]
    public async Task SavePhoto_FakeJpgTextContent_ReturnsUnsupportedTypeAndNoFile()
    {
        var textBytes = System.Text.Encoding.UTF8.GetBytes("bu bir metin dosyasidir");
        var result = await _photos.SavePhotoAsync(Request(textBytes, "sahte-fotograf.jpg", "image/jpeg"));

        Assert.False(result.Success);
        Assert.Equal(PhotoUploadErrorCode.InvalidSignature, result.ErrorCode);
        Assert.Equal(PhotoUploadMessages.UnsupportedType, result.UserMessage);
        Assert.Empty(Directory.EnumerateFiles(_storageRoot));
    }

    [Fact]
    public async Task SavePhoto_UnsupportedExtension_ReturnsUnsupportedTypeMessage()
    {
        var result = await _photos.SavePhotoAsync(Request(PhotoUploadTestSupport.MinimalJpeg, "photo.gif", "image/gif"));

        Assert.False(result.Success);
        Assert.Equal(PhotoUploadErrorCode.UnsupportedExtension, result.ErrorCode);
        Assert.Equal(PhotoUploadMessages.UnsupportedType, result.UserMessage);
    }

    [Theory]
    [InlineData("notes.txt", "text/plain")]
    [InlineData("doc.pdf", "application/pdf")]
    [InlineData("photo.webp", "image/webp")]
    [InlineData("photo.svg", "image/svg+xml")]
    [InlineData("photo.bmp", "image/bmp")]
    public async Task SavePhoto_RejectedExtensions_ReturnUnsupportedType(string fileName, string contentType)
    {
        var result = await _photos.SavePhotoAsync(Request(PhotoUploadTestSupport.MinimalJpeg, fileName, contentType));

        Assert.False(result.Success);
        Assert.Equal(PhotoUploadMessages.UnsupportedType, result.UserMessage);
        Assert.Empty(Directory.EnumerateFiles(_storageRoot));
    }

    [Fact]
    public async Task SavePhoto_DoubleExtension_IsRejected()
    {
        var result = await _photos.SavePhotoAsync(Request(
            PhotoUploadTestSupport.MinimalJpeg, "document.pdf.jpg", "image/jpeg"));

        Assert.False(result.Success);
        Assert.Equal(PhotoUploadErrorCode.InvalidFileName, result.ErrorCode);
        Assert.Equal(PhotoUploadMessages.UnsupportedType, result.UserMessage);
    }

    [Fact]
    public async Task SavePhoto_TooLarge_ReturnsSizeMessage()
    {
        var content = PhotoUploadTestSupport.CreateJpegOfLength((int)PhotoUploadLimits.MaxFileBytes + 1);

        var result = await _photos.SavePhotoAsync(Request(content, "photo.jpg", "image/jpeg"));

        Assert.False(result.Success);
        Assert.Equal(PhotoUploadErrorCode.TooLarge, result.ErrorCode);
        Assert.Equal(PhotoUploadMessages.TooLarge, result.UserMessage);
    }

    [Fact]
    public async Task SavePhoto_ExactMaxBytesValidJpeg_Succeeds()
    {
        var content = PhotoUploadTestSupport.CreateJpegOfLength((int)PhotoUploadLimits.MaxFileBytes);

        var result = await _photos.SavePhotoAsync(Request(content, "photo.jpg", "image/jpeg"));

        Assert.True(result.Success);
        Assert.NotEmpty(Directory.EnumerateFiles(_storageRoot));
    }

    [Fact]
    public async Task SavePhoto_MaxBytesMinusOneValidJpeg_Succeeds()
    {
        var content = PhotoUploadTestSupport.CreateJpegOfLength((int)PhotoUploadLimits.MaxFileBytes - 1);

        var result = await _photos.SavePhotoAsync(Request(content, "photo.jpg", "image/jpeg"));

        Assert.True(result.Success);
    }

    [Fact]
    public async Task SavePhoto_ValidJpegAndPng_Succeed()
    {
        var jpeg = await _photos.SavePhotoAsync(Request(PhotoUploadTestSupport.MinimalJpeg, "photo.jpg", "image/jpeg"));
        var png = await _photos.SavePhotoAsync(Request(PhotoUploadTestSupport.MinimalPng, "photo.png", "image/png"));

        Assert.True(jpeg.Success);
        Assert.True(png.Success);
        Assert.Equal(PhotoUploadErrorCode.None, jpeg.ErrorCode);
        Assert.StartsWith("/uploads/photos/", jpeg.StoredPath);
        Assert.EndsWith(".png", png.StoredPath);
        Assert.Matches(@"^/uploads/photos/[0-9a-fA-F]{32}\.(jpg|png)$", jpeg.StoredPath!);
    }

    [Fact]
    public async Task SavePhoto_UppercaseExtension_Succeeds()
    {
        var result = await _photos.SavePhotoAsync(Request(
            PhotoUploadTestSupport.MinimalJpeg, "IMAGE.JPG", "image/jpeg"));

        Assert.True(result.Success);
    }

    [Fact]
    public async Task SavePhoto_EmptyContent_ReturnsEmptyMessage()
    {
        var result = await _photos.SavePhotoAsync(Request(Array.Empty<byte>(), "photo.jpg", "image/jpeg"));

        Assert.False(result.Success);
        Assert.Equal(PhotoUploadErrorCode.Empty, result.ErrorCode);
        Assert.Equal(PhotoUploadMessages.Empty, result.UserMessage);
    }

    [Fact]
    public async Task SavePhoto_TruncatedJpeg_ReturnsInvalidContent()
    {
        var result = await _photos.SavePhotoAsync(Request(
            PhotoUploadTestSupport.TruncatedJpegHeaderOnly(), "photo.jpg", "image/jpeg"));

        Assert.False(result.Success);
        Assert.Equal(PhotoUploadMessages.InvalidContent, result.UserMessage);
        Assert.Empty(Directory.EnumerateFiles(_storageRoot));
    }

    [Fact]
    public async Task SavePhoto_TruncatedPng_ReturnsInvalidContent()
    {
        var result = await _photos.SavePhotoAsync(Request(
            PhotoUploadTestSupport.TruncatedPngHeaderOnly(), "photo.png", "image/png"));

        Assert.False(result.Success);
        Assert.Equal(PhotoUploadMessages.InvalidContent, result.UserMessage);
        Assert.Empty(Directory.EnumerateFiles(_storageRoot));
    }

    [Fact]
    public async Task SavePhoto_FakeContentTypePdfBytesAsJpeg_IsRejected()
    {
        var pdf = System.Text.Encoding.ASCII.GetBytes("%PDF-1.4 fake");
        var result = await _photos.SavePhotoAsync(Request(pdf, "photo.jpg", "image/jpeg"));

        Assert.False(result.Success);
        Assert.Equal(PhotoUploadMessages.UnsupportedType, result.UserMessage);
        Assert.Empty(Directory.EnumerateFiles(_storageRoot));
    }

    [Fact]
    public async Task ValidatePhoto_DoesNotWriteStorage()
    {
        var result = await _photos.ValidatePhotoAsync(Request(
            PhotoUploadTestSupport.MinimalJpeg, "photo.jpg", "image/jpeg"));

        Assert.True(result.Success);
        Assert.Empty(Directory.EnumerateFiles(_storageRoot));
    }

    [Fact]
    public async Task RegisterCandidate_MissingPhoto_ReturnsRequiredMessage()
    {
        var sut = CreateAuthService(out var userRepo);
        var result = await sut.RegisterCandidateAsync(ValidRegister(Guid.NewGuid()), photo: null, null, null);

        Assert.False(result.Success);
        Assert.Contains(PhotoUploadMessages.Required, result.ValidationErrors);
        userRepo.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisterCandidate_FakeJpg_PropagatesUnsupportedType_NotRequired()
    {
        var ygsYearId = Guid.NewGuid();
        var sut = CreateAuthService(out var userRepo, ygsYearId);
        var textBytes = System.Text.Encoding.UTF8.GetBytes("degil-resim");

        var result = await sut.RegisterCandidateAsync(
            ValidRegister(ygsYearId),
            Request(textBytes, "sahte-fotograf.jpg", "image/jpeg"),
            null,
            null);

        Assert.False(result.Success);
        Assert.Equal(PhotoUploadMessages.UnsupportedType, result.ErrorMessage);
        Assert.DoesNotContain(PhotoUploadMessages.Required, result.ValidationErrors);
        userRepo.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Empty(Directory.EnumerateFiles(_storageRoot));
    }

    [Fact]
    public void RegisterView_UsesMultipartAspForPhotoAndValidation()
    {
        var view = File.ReadAllText(FindUnder("src", "OzelYetenekSinavSistemi.Web", "Views", "Account", "Register.cshtml"));
        Assert.Contains("enctype=\"multipart/form-data\"", view, StringComparison.Ordinal);
        Assert.Contains("asp-for=\"Photo\"", view, StringComparison.Ordinal);
        Assert.Contains("asp-validation-for=\"Photo\"", view, StringComparison.Ordinal);
        Assert.Contains("Vesikalık Fotoğraf", view, StringComparison.Ordinal);
        Assert.Contains("PhotoUploadLimits.AcceptAttribute", view, StringComparison.Ordinal);
        Assert.Contains("PhotoUploadLimits.MaxFileBytes", view, StringComparison.Ordinal);
        Assert.Contains("PhotoUploadLimits.MaxFileSizeDisplay", view, StringComparison.Ordinal);
        Assert.Contains("data-oys-photo-input", view, StringComparison.Ordinal);
        Assert.Contains("data-oys-photo-pick", view, StringComparison.Ordinal);
        Assert.Contains("data-oys-photo-status", view, StringComparison.Ordinal);
        Assert.Contains("data-oys-photo-preview", view, StringComparison.Ordinal);
        Assert.Contains("PhotoUploadMessages.ReselectHint", view, StringComparison.Ordinal);
        Assert.Contains("Son 6 ay içinde çekilmiş", view, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"photo\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("onchange=", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onclick=", view, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(1, CountOccurrences(view, "type=\"file\""));
        Assert.Equal(1, CountOccurrences(view, "asp-validation-for=\"Photo\""));
        Assert.Equal(2, CountOccurrences(view, "asp-for=\"Photo\""));
    }

    [Fact]
    public void AuthLayout_DoesNotRenderPhotoFileInput()
    {
        var layout = File.ReadAllText(FindUnder("src", "OzelYetenekSinavSistemi.Web", "Views", "Shared", "_AuthLayout.cshtml"));
        Assert.DoesNotContain("type=\"file\"", layout, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("asp-for=\"Photo\"", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"Photo\"", layout, StringComparison.Ordinal);
    }

    [Fact]
    public void SiteJs_PhotoFeedback_ValidatesTypeSizeSignatureAndClearsOnInvalid()
    {
        var js = File.ReadAllText(FindUnder("src", "OzelYetenekSinavSistemi.Web", "wwwroot", "js", "site.js"));
        Assert.Contains("oysBindPhotoFileFeedback", js, StringComparison.Ordinal);

        var photoFnStart = js.IndexOf("function oysBindPhotoFileFeedback", StringComparison.Ordinal);
        var nextFn = js.IndexOf("\nfunction oysParseBoolParam", photoFnStart, StringComparison.Ordinal);
        Assert.True(photoFnStart >= 0 && nextFn > photoFnStart);
        var photoFn = js.Substring(photoFnStart, nextFn - photoFnStart);

        Assert.Contains("data-oys-photo-max-bytes", photoFn, StringComparison.Ordinal);
        Assert.Contains("Yalnızca geçerli JPG, JPEG veya PNG fotoğraf yükleyiniz.", photoFn, StringComparison.Ordinal);
        Assert.Contains("Fotoğraf dosyası en fazla 2 MB olabilir.", photoFn, StringComparison.Ordinal);
        Assert.Contains("Seçilen fotoğraf dosyası boş olamaz.", photoFn, StringComparison.Ordinal);
        Assert.Contains("Fotoğraf seçilmedi", photoFn, StringComparison.Ordinal);
        Assert.Contains("Fotoğrafı Değiştir", photoFn, StringComparison.Ordinal);
        Assert.Contains("0xFF", photoFn, StringComparison.Ordinal);
        Assert.Contains("0x89", photoFn, StringComparison.Ordinal);
        Assert.Contains("input.value = \"\"", photoFn, StringComparison.Ordinal);
        Assert.Contains("readAsDataURL", photoFn, StringComparison.Ordinal);
        Assert.Contains("data:image/", photoFn, StringComparison.Ordinal);
        Assert.Contains("setCustomValidity", photoFn, StringComparison.Ordinal);
        Assert.Contains("previewToken", photoFn, StringComparison.Ordinal);
        Assert.DoesNotContain("URL.createObjectURL", photoFn, StringComparison.Ordinal);
        Assert.DoesNotContain("innerHTML", photoFn, StringComparison.Ordinal);
    }

    [Fact]
    public void SiteJs_PhotoPreview_UsesDataUrlCompatibleWithCspImgSrc()
    {
        var js = File.ReadAllText(FindUnder("src", "OzelYetenekSinavSistemi.Web", "wwwroot", "js", "site.js"));
        var csp = File.ReadAllText(FindUnder(
            "src", "OzelYetenekSinavSistemi.Web", "Infrastructure", "SecurityHeadersMiddleware.cs"));
        var photoFnStart = js.IndexOf("function oysBindPhotoFileFeedback", StringComparison.Ordinal);
        var nextFn = js.IndexOf("\nfunction oysParseBoolParam", photoFnStart, StringComparison.Ordinal);
        var photoFn = js.Substring(photoFnStart, nextFn - photoFnStart);

        Assert.Contains("img-src 'self' data:", csp, StringComparison.Ordinal);
        Assert.DoesNotContain("createObjectURL", photoFn, StringComparison.Ordinal);
        Assert.DoesNotContain("revokeObjectURL", photoFn, StringComparison.Ordinal);
        Assert.Contains("reader.readAsDataURL(file)", photoFn, StringComparison.Ordinal);
        Assert.Contains("preview.src = dataUrl", photoFn, StringComparison.Ordinal);
        Assert.Contains("preview.onload", photoFn, StringComparison.Ordinal);
        Assert.Contains("preview.onerror", photoFn, StringComparison.Ordinal);
        Assert.Contains("clearPreview()", photoFn, StringComparison.Ordinal);
        Assert.Contains("removeAttribute(\"src\")", photoFn, StringComparison.Ordinal);
        Assert.Contains("Seçilen fotoğraf dosyası okunamadı. Lütfen geçerli bir fotoğraf seçiniz.", photoFn, StringComparison.Ordinal);
        Assert.Contains("Önizleme yükleniyor", File.ReadAllText(FindUnder(
            "src", "OzelYetenekSinavSistemi.Web", "Views", "Account", "Register.cshtml")), StringComparison.Ordinal);
    }

    [Fact]
    public void RegisterView_PhotoPreviewMarkup_HidesBrokenImageUntilLoaded()
    {
        var view = File.ReadAllText(FindUnder("src", "OzelYetenekSinavSistemi.Web", "Views", "Account", "Register.cshtml"));
        Assert.Contains("data-oys-photo-preview-wrap", view, StringComparison.Ordinal);
        Assert.Contains("data-oys-photo-preview-loading", view, StringComparison.Ordinal);
        Assert.Contains("data-oys-photo-preview", view, StringComparison.Ordinal);
        Assert.Contains("alt=\"Seçilen vesikalık fotoğraf önizlemesi\"", view, StringComparison.Ordinal);
        Assert.Contains("object-fit: contain", view, StringComparison.Ordinal);
        Assert.Contains(".oys-photo-preview[hidden]", view, StringComparison.Ordinal);
    }

    [Fact]
    public void LimitsAndMessages_UseSingleTwoMegabyteCeiling()
    {
        Assert.Equal(2_097_152, PhotoUploadLimits.MaxFileBytes);
        Assert.Equal("2 MB", PhotoUploadLimits.MaxFileSizeDisplay);
        Assert.Contains("2 MB", PhotoUploadMessages.TooLarge, StringComparison.Ordinal);
        Assert.Equal(PhotoUploadLimits.AcceptAttribute, ".jpg,.jpeg,.png,image/jpeg,image/png");
    }

    [Fact]
    public void RegisterViewModel_HasIFormFilePhotoProperty()
    {
        var prop = typeof(RegisterViewModel).GetProperty(nameof(RegisterViewModel.Photo));
        Assert.NotNull(prop);
        Assert.Equal(typeof(IFormFile), prop!.PropertyType);
    }

    [Fact]
    public async Task RegisterPost_MissingPhoto_AddsRequiredOnPhotoField()
    {
        var controller = CreateController(out _, out _);
        var model = ValidRegister(Guid.NewGuid());
        model.Photo = null;

        var result = await controller.Register(model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains(
            controller.ModelState[nameof(RegisterViewModel.Photo)]!.Errors,
            e => e.ErrorMessage == PhotoUploadMessages.Required);
        Assert.Same(model, view.Model);
    }

    [Fact]
    public async Task RegisterPost_TxtPhoto_MapsUnsupportedTypeEvenWhenOtherFieldsInvalid()
    {
        var ygsYearId = Guid.NewGuid();
        var controller = CreateController(out var userRepo, out _, ygsYearId);
        var model = ValidRegister(ygsYearId);
        model.FirstName = "";
        model.Photo = FormFile(System.Text.Encoding.UTF8.GetBytes("plain"), "notes.txt", "text/plain");

        var result = await controller.Register(model, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Contains(
            controller.ModelState[nameof(RegisterViewModel.Photo)]!.Errors,
            e => e.ErrorMessage == PhotoUploadMessages.UnsupportedType);
        userRepo.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Empty(Directory.EnumerateFiles(_storageRoot));
    }

    [Fact]
    public async Task RegisterPost_FakeJpg_MapsUnsupportedTypeToPhoto_NotRequired()
    {
        var ygsYearId = Guid.NewGuid();
        var controller = CreateController(out var userRepo, out _, ygsYearId);
        var model = ValidRegister(ygsYearId);
        model.Photo = FormFile(System.Text.Encoding.UTF8.GetBytes("sahte"), "sahte-fotograf.jpg", "image/jpeg");

        var result = await controller.Register(model, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Contains(
            controller.ModelState[nameof(RegisterViewModel.Photo)]!.Errors,
            e => e.ErrorMessage == PhotoUploadMessages.UnsupportedType);
        Assert.DoesNotContain(
            controller.ModelState[nameof(RegisterViewModel.Photo)]!.Errors,
            e => e.ErrorMessage == PhotoUploadMessages.Required);
        userRepo.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(string.Empty, model.Password);
        Assert.Equal(string.Empty, model.ConfirmPassword);
        Assert.Null(model.Photo);
        Assert.Equal("Ali", model.FirstName);
        Assert.Equal("ali@example.com", model.Email);
        Assert.Equal(new DateOnly(2000, 6, 15), model.BirthDate);
        Assert.Empty(Directory.EnumerateFiles(_storageRoot));
    }

    [Fact]
    public async Task RegisterPost_WhenPhotoPresent_ClearsStaleRequiredThenAddsUploadError()
    {
        var ygsYearId = Guid.NewGuid();
        var controller = CreateController(out _, out _, ygsYearId);

        controller.ModelState.AddModelError(nameof(RegisterViewModel.Photo), PhotoUploadMessages.Required);
        controller.ModelState.AddModelError(nameof(RegisterViewModel.Photo), "eski-photo-hatasi");

        var model = ValidRegister(ygsYearId);
        model.Photo = FormFile(System.Text.Encoding.UTF8.GetBytes("not-image"), "fake.jpg", "image/jpeg");

        _ = await controller.Register(model, CancellationToken.None);

        var photoErrors = controller.ModelState[nameof(RegisterViewModel.Photo)]!.Errors
            .Select(e => e.ErrorMessage)
            .ToList();

        Assert.DoesNotContain(PhotoUploadMessages.Required, photoErrors);
        Assert.DoesNotContain("eski-photo-hatasi", photoErrors);
        Assert.Equal(PhotoUploadMessages.UnsupportedType, Assert.Single(photoErrors));
    }

    [Fact]
    public async Task RegisterPost_WrongFieldNameOnly_TreatsPhotoAsMissing()
    {
        var controller = CreateController(out var userRepo, out _);
        var model = ValidRegister(Guid.NewGuid());
        model.Photo = null;

        _ = await controller.Register(model, CancellationToken.None);

        Assert.Contains(
            controller.ModelState[nameof(RegisterViewModel.Photo)]!.Errors,
            e => e.ErrorMessage == PhotoUploadMessages.Required);
        userRepo.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private AccountController CreateController(
        out Mock<IUserRepository> userRepo,
        out Mock<IYgsYearRepository> ygsRepo,
        Guid? ygsYearId = null)
    {
        var auth = CreateAuthService(out userRepo, ygsYearId);
        ygsRepo = new Mock<IYgsYearRepository>();
        var yearId = ygsYearId ?? Guid.NewGuid();
        ygsRepo.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<YgsYear> { new() { Id = yearId, Year = 2024 } });
        ygsRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => new YgsYear { Id = id });

        var controller = new AccountController(
            auth,
            Mock.Of<IUserService>(),
            Mock.Of<IPasswordResetService>(),
            Mock.Of<ICaptchaService>(),
            ygsRepo.Object,
            Mock.Of<IPublicUrlBuilder>(),
            new CountryCatalog(),
            new IdentityDocumentValidator(TimeProvider.System),
            _photos)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            },
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
        };

        return controller;
    }

    private AuthenticationService CreateAuthService(out Mock<IUserRepository> userRepo, Guid? ygsYearId = null)
    {
        userRepo = new Mock<IUserRepository>(MockBehavior.Strict);
        var ygsRepo = new Mock<IYgsYearRepository>();
        var password = new Mock<IPasswordService>();
        var audit = new Mock<IAuditService>();
        var masking = new Mock<ISensitiveDataMaskingService>();

        ygsRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => new YgsYear { Id = id });
        userRepo.Setup(r => r.IdentityExistsAsync(It.IsAny<Domain.Enums.IdentityDocumentType>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        userRepo.Setup(r => r.TcNoExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        userRepo.Setup(r => r.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        masking.Setup(m => m.MaskTcNo(It.IsAny<string>())).Returns("***********");

        return new AuthenticationService(
            userRepo.Object,
            ygsRepo.Object,
            password.Object,
            new IdentityDocumentValidator(TimeProvider.System),
            _photos,
            audit.Object,
            masking.Object,
            PhotoUploadTestSupport.SharedLock,
            TimeProvider.System);
    }

    private static RegisterViewModel ValidRegister(Guid ygsYearId) => new()
    {
        FirstName = "Ali",
        LastName = "Veli",
        IdentityDocumentType = Domain.Enums.IdentityDocumentType.TurkishIdentityNumber,
        IdentityNumber = "10000000146",
        NationalityCountryCode = "TR",
        Email = "ali@example.com",
        BirthDate = new DateOnly(2000, 6, 15),
        Phone = "05321234567",
        Password = "Passw0rd!",
        ConfirmPassword = "Passw0rd!",
        YgsScore = 250,
        YgsYearId = ygsYearId
    };

    private static PhotoUploadRequest Request(byte[] content, string fileName, string contentType) => new()
    {
        Content = new MemoryStream(content),
        FileName = fileName,
        ContentType = contentType,
        Length = content.Length
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

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }

    private static string FindUnder(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(new[] { dir.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate) || Directory.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException(string.Join(Path.DirectorySeparatorChar, parts));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_storageRoot))
                Directory.Delete(_storageRoot, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
