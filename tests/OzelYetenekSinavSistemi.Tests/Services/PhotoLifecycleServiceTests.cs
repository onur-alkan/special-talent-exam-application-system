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

namespace OzelYetenekSinavSistemi.Tests.Services;

public sealed class PhotoLifecycleServiceTests
{
    private static readonly byte[] JpegHeader = PhotoUploadTestSupport.MinimalJpeg;

    [Fact]
    public async Task RegisterCandidate_WhenAddAsyncFails_DeletesNewPhoto()
    {
        var ygsYearId = Guid.NewGuid();
        var photoPath = $"/uploads/photos/{Guid.NewGuid():N}.jpg";

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
            .ReturnsAsync(PhotoUploadResult.Ok(photoPath));
        userRepo.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db failure"));
        photos.Setup(p => p.DeletePhotoAsync(photoPath, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        masking.Setup(m => m.MaskTcNo(It.IsAny<string>())).Returns("***********");

        var sut = new AuthenticationService(
            userRepo.Object, ygsRepo.Object, password.Object, identityValidator, photos.Object, audit.Object, masking.Object,
            PhotoUploadTestSupport.SharedLock,
            TimeProvider.System);

        var result = await sut.RegisterCandidateAsync(ValidRegisterModel(ygsYearId), ValidPhoto(), null, null);

        Assert.False(result.Success);
        Assert.DoesNotContain("db failure", result.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        photos.Verify(p => p.DeletePhotoAsync(photoPath, It.IsAny<CancellationToken>()), Times.Once);
        audit.Verify(
            a => a.LogAsync("CandidateRegistered", It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateProfile_WhenUpdateFails_DeletesNewPhotoAndKeepsOld()
    {
        var userId = Guid.NewGuid();
        var oldPath = $"/uploads/photos/{Guid.NewGuid():N}.jpg";
        var newPath = $"/uploads/photos/{Guid.NewGuid():N}.jpg";

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
                PhotoPath = oldPath
            });
        photos.Setup(p => p.SavePhotoAsync(It.IsAny<PhotoUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PhotoUploadResult.Ok(newPath));
        userRepo.Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        photos.Setup(p => p.DeletePhotoAsync(newPath, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var sut = new UserService(
            userRepo.Object, ygsRepo.Object, photos.Object, password.Object, audit.Object, new CountryCatalog(), NullLogger<UserService>.Instance,
            PhotoUploadTestSupport.SharedLock);

        var result = await sut.UpdateProfileAsync(userId, ValidProfile("old@example.com"), ValidPhoto());

        Assert.False(result.Success);
        photos.Verify(p => p.DeletePhotoAsync(newPath, It.IsAny<CancellationToken>()), Times.Once);
        photos.Verify(p => p.DeletePhotoAsync(oldPath, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateProfile_WhenUpdateSucceeds_DeletesOldPhoto()
    {
        var userId = Guid.NewGuid();
        var oldPath = $"/uploads/photos/{Guid.NewGuid():N}.jpg";
        var newPath = $"/uploads/photos/{Guid.NewGuid():N}.jpg";

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
                PhotoPath = oldPath
            });
        photos.Setup(p => p.SavePhotoAsync(It.IsAny<PhotoUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PhotoUploadResult.Ok(newPath));
        userRepo.Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        photos.Setup(p => p.DeletePhotoAsync(oldPath, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var sut = new UserService(
            userRepo.Object, ygsRepo.Object, photos.Object, password.Object, audit.Object, new CountryCatalog(), NullLogger<UserService>.Instance,
            PhotoUploadTestSupport.SharedLock);

        var result = await sut.UpdateProfileAsync(userId, ValidProfile("old@example.com"), ValidPhoto());

        Assert.True(result.Success);
        photos.Verify(p => p.DeletePhotoAsync(oldPath, It.IsAny<CancellationToken>()), Times.Once);
        photos.Verify(p => p.DeletePhotoAsync(newPath, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateProfile_WithoutNewPhoto_DoesNotDeleteExisting()
    {
        var userId = Guid.NewGuid();
        var oldPath = $"/uploads/photos/{Guid.NewGuid():N}.jpg";

        var userRepo = new Mock<IUserRepository>();
        var ygsRepo = new Mock<IYgsYearRepository>();
        var photos = new Mock<IPhotoUploadService>(MockBehavior.Strict);
        var password = new Mock<IPasswordService>();
        var audit = new Mock<IAuditService>();

        userRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                Id = userId,
                Email = "old@example.com",
                FirstName = "Ali",
                LastName = "Veli",
                PhotoPath = oldPath
            });
        userRepo.Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        audit.Setup(a => a.LogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new UserService(
            userRepo.Object, ygsRepo.Object, photos.Object, password.Object, audit.Object, new CountryCatalog(), NullLogger<UserService>.Instance,
            PhotoUploadTestSupport.SharedLock);

        var result = await sut.UpdateProfileAsync(userId, ValidProfile("old@example.com"), photo: null);

        Assert.True(result.Success);
        photos.Verify(p => p.DeletePhotoAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        photos.Verify(p => p.SavePhotoAsync(It.IsAny<PhotoUploadRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static RegisterViewModel ValidRegisterModel(Guid ygsYearId) => new()
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
}
