using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Application.Services;
using OzelYetenekSinavSistemi.Application.ViewModels.Account;
using OzelYetenekSinavSistemi.Application.ViewModels.Profile;
using OzelYetenekSinavSistemi.Domain.Entities;
using OzelYetenekSinavSistemi.Web.Infrastructure;

namespace OzelYetenekSinavSistemi.Tests.Services;

public sealed class DisabilityDetailsIntegrityTests
{
    private static readonly byte[] JpegHeader = PhotoUploadTestSupport.MinimalJpeg;

    [Fact]
    public async Task RegisterCandidate_WhenDisabilityUnchecked_PersistsNullDetailsEvenIfPosted()
    {
        var ygsYearId = Guid.NewGuid();
        User? saved = null;

        var userRepo = new Mock<IUserRepository>();
        var ygsRepo = new Mock<IYgsYearRepository>();
        var password = new Mock<IPasswordService>();
        var identityValidator = new IdentityDocumentValidator(TimeProvider.System);
        var photos = new Mock<IPhotoUploadService>();
        var audit = new Mock<IAuditService>();
        var masking = new Mock<ISensitiveDataMaskingService>();

        ygsRepo.Setup(r => r.GetByIdAsync(ygsYearId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new YgsYear { Id = ygsYearId });
        userRepo.Setup(r => r.IdentityExistsAsync(It.IsAny<Domain.Enums.IdentityDocumentType>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        userRepo.Setup(r => r.TcNoExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        userRepo.Setup(r => r.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        password.Setup(p => p.Hash(It.IsAny<string>())).Returns("hash");
        photos.Setup(p => p.SavePhotoAsync(It.IsAny<PhotoUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PhotoUploadResult.Ok($"/uploads/photos/{Guid.NewGuid():N}.jpg"));
        userRepo.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => saved = u)
            .ReturnsAsync(Guid.NewGuid());
        masking.Setup(m => m.MaskTcNo(It.IsAny<string>())).Returns("***********");
        audit.Setup(a => a.LogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new AuthenticationService(
            userRepo.Object, ygsRepo.Object, password.Object, identityValidator, photos.Object, audit.Object, masking.Object,
            PhotoUploadTestSupport.SharedLock,
            TimeProvider.System);

        var model = ValidRegister(ygsYearId);
        model.HasDisability = false;
        model.DisabilityDetails = "İstemci manipülasyonu";

        var result = await sut.RegisterCandidateAsync(model, ValidPhoto(), null, null);

        Assert.True(result.Success);
        Assert.NotNull(saved);
        Assert.False(saved!.HasDisability);
        Assert.Null(saved.DisabilityDetails);
    }

    [Fact]
    public async Task UpdateProfile_WhenDisabilityUnchecked_PersistsNullDetailsEvenIfPosted()
    {
        var userId = Guid.NewGuid();
        User? updated = null;

        var userRepo = new Mock<IUserRepository>();
        var ygsRepo = new Mock<IYgsYearRepository>();
        var photos = new Mock<IPhotoUploadService>();
        var password = new Mock<IPasswordService>();
        var audit = new Mock<IAuditService>();

        userRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                Id = userId,
                Email = "old@example.com",
                FirstName = "Ali",
                LastName = "Veli",
                HasDisability = true,
                DisabilityDetails = "Önceki açıklama"
            });
        userRepo.Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => updated = u)
            .ReturnsAsync(true);
        audit.Setup(a => a.LogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new UserService(
            userRepo.Object, ygsRepo.Object, photos.Object, password.Object, audit.Object, new CountryCatalog(), NullLogger<UserService>.Instance,
            PhotoUploadTestSupport.SharedLock);

        var model = ValidProfile("old@example.com");
        model.HasDisability = false;
        model.DisabilityDetails = "İstemci manipülasyonu";

        var result = await sut.UpdateProfileAsync(userId, model, photo: null);

        Assert.True(result.Success);
        Assert.NotNull(updated);
        Assert.False(updated!.HasDisability);
        Assert.Null(updated.DisabilityDetails);
    }

    [Fact]
    public async Task UpdateProfile_WhenDisabilityCheckedWithoutDetails_Fails()
    {
        var userId = Guid.NewGuid();
        var userRepo = new Mock<IUserRepository>();
        var ygsRepo = new Mock<IYgsYearRepository>();
        var photos = new Mock<IPhotoUploadService>();
        var password = new Mock<IPasswordService>();
        var audit = new Mock<IAuditService>();

        userRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                Id = userId,
                Email = "old@example.com",
                FirstName = "Ali",
                LastName = "Veli"
            });

        var sut = new UserService(
            userRepo.Object, ygsRepo.Object, photos.Object, password.Object, audit.Object, new CountryCatalog(), NullLogger<UserService>.Instance,
            PhotoUploadTestSupport.SharedLock);

        var model = ValidProfile("old@example.com");
        model.HasDisability = true;
        model.DisabilityDetails = " ";

        var result = await sut.UpdateProfileAsync(userId, model, photo: null);

        Assert.False(result.Success);
        Assert.Equal(DisabilityDetailsNormalizer.RequiredMessage, result.ErrorMessage);
        userRepo.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void DisabilityFormBinding_Normalize_ClearsDetailsAndModelState()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("DisabilityDetails", "too long");
        string? details = "eski metin";

        DisabilityFormBinding.Normalize(false, value => details = value, modelState);

        Assert.Null(details);
        Assert.False(modelState.ContainsKey("DisabilityDetails"));
    }

    [Fact]
    public void SiteJs_ClearsAndDisablesDisabilityDetailsWhenUnchecked()
    {
        var siteJs = File.ReadAllText(FindUnder("src", "OzelYetenekSinavSistemi.Web", "wwwroot", "js", "site.js"));
        Assert.Contains("oysBindDisabilityToggle", siteJs, StringComparison.Ordinal);
        Assert.Contains("js-disability-toggle", siteJs, StringComparison.Ordinal);
        Assert.Contains("data-disability-container", siteJs, StringComparison.Ordinal);
        Assert.Contains("data-disability-field", siteJs, StringComparison.Ordinal);
        Assert.Contains("container.hidden = !enabled", siteJs, StringComparison.Ordinal);
        Assert.Contains("field.disabled = !enabled", siteJs, StringComparison.Ordinal);
        Assert.Contains("field.value = \"\"", siteJs, StringComparison.Ordinal);
        Assert.Contains("aria-hidden", siteJs, StringComparison.Ordinal);
        Assert.Contains("setCustomValidity", siteJs, StringComparison.Ordinal);
        Assert.Contains("site-hidden", siteJs, StringComparison.Ordinal);
        Assert.Contains("input-validation-error", siteJs, StringComparison.Ordinal);
    }

    [Fact]
    public void RegisterAndProfileViews_UseSharedDisabilityToggleMarkup()
    {
        var register = File.ReadAllText(FindUnder("src", "OzelYetenekSinavSistemi.Web", "Views", "Account", "Register.cshtml"));
        var profile = File.ReadAllText(FindUnder("src", "OzelYetenekSinavSistemi.Web", "Views", "CandidateProfile", "Index.cshtml"));

        foreach (var view in new[] { register, profile })
        {
            Assert.Contains("js-disability-toggle", view, StringComparison.Ordinal);
            Assert.Contains("data-disability-container=\"#disability-description-container\"", view, StringComparison.Ordinal);
            Assert.Contains("data-disability-field=\"#DisabilityDetails\"", view, StringComparison.Ordinal);
            Assert.Contains("id=\"disability-description-container\"", view, StringComparison.Ordinal);
            Assert.Contains("data-disability-description-container", view, StringComparison.Ordinal);
            Assert.Contains("asp-for=\"DisabilityDetails\"", view, StringComparison.Ordinal);
            Assert.Contains("asp-validation-for=\"DisabilityDetails\"", view, StringComparison.Ordinal);
            Assert.Contains("aria-hidden=", view, StringComparison.Ordinal);
            Assert.DoesNotContain("site-hidden", view, StringComparison.Ordinal);
            Assert.DoesNotContain("id=\"disabilityDetailsGroup\"", view, StringComparison.Ordinal);
        }
    }

    private static RegisterViewModel ValidRegister(Guid ygsYearId) => new()
    {
        FirstName = "Ali",
        LastName = "Veli",
        TcNo = "10000000146",
        Email = "ali@example.com",
        BirthDate = new DateOnly(2000, 6, 15),
        Phone = "5321234567",
        Password = "Passw0rd!",
        ConfirmPassword = "Passw0rd!",
        YgsScore = 250,
        YgsYearId = ygsYearId
    };

    private static ProfileUpdateViewModel ValidProfile(string email) => new()
    {
        FirstName = "Ali",
        LastName = "Veli",
        Email = email,
        Phone = "5321234567"
    };

    private static PhotoUploadRequest ValidPhoto() => new()
    {
        Content = new MemoryStream(JpegHeader),
        FileName = "photo.jpg",
        ContentType = "image/jpeg",
        Length = JpegHeader.Length
    };

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
}
