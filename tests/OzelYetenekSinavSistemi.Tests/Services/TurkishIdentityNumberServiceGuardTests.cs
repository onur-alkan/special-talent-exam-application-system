using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.DTOs.Users;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Application.Services;
using OzelYetenekSinavSistemi.Application.ViewModels.Account;
using OzelYetenekSinavSistemi.Application.ViewModels.UserManagement;
using OzelYetenekSinavSistemi.Domain.Common;
using OzelYetenekSinavSistemi.Domain.Entities;

namespace OzelYetenekSinavSistemi.Tests.Services;

public sealed class TurkishIdentityNumberServiceGuardTests
{
    private static readonly byte[] JpegHeader = PhotoUploadTestSupport.MinimalJpeg;

    [Fact]
    public async Task RegisterCandidate_RejectsAlgorithmicallyInvalidTcNo_WithoutPersisting()
    {
        var ygsYearId = Guid.NewGuid();
        var userRepo = new Mock<IUserRepository>(MockBehavior.Strict);
        var ygsRepo = new Mock<IYgsYearRepository>();
        var password = new Mock<IPasswordService>(MockBehavior.Strict);
        var photos = new Mock<IPhotoUploadService>(MockBehavior.Strict);
        var audit = new Mock<IAuditService>(MockBehavior.Strict);
        var masking = new Mock<ISensitiveDataMaskingService>(MockBehavior.Strict);

        ygsRepo.Setup(r => r.GetByIdAsync(ygsYearId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new YgsYear { Id = ygsYearId });

        var sut = new AuthenticationService(
            userRepo.Object,
            ygsRepo.Object,
            password.Object,
            new IdentityDocumentValidator(TimeProvider.System),
            photos.Object,
            audit.Object,
            masking.Object,
            PhotoUploadTestSupport.SharedLock,
            TimeProvider.System);

        var model = new RegisterViewModel
        {
            FirstName = "Ali",
            LastName = "Veli",
            BirthDate = new DateOnly(2000, 6, 15),
            TcNo = "12345678901",
            Email = "ali@example.com",
            Phone = "05321234567",
            Password = "Passw0rd!",
            ConfirmPassword = "Passw0rd!",
            YgsScore = 250,
            YgsYearId = ygsYearId
        };

        var result = await sut.RegisterCandidateAsync(model, ValidPhoto(), null, null);

        Assert.False(result.Success);
        Assert.Contains(TurkishIdentityNumber.InvalidMessage, result.ValidationErrors);
        Assert.DoesNotContain("12345678901", result.ErrorMessage ?? string.Empty, StringComparison.Ordinal);
        userRepo.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateStaffUser_RejectsAlgorithmicallyInvalidTcNo_WithoutPersisting()
    {
        var users = new Mock<IUserRepository>(MockBehavior.Strict);
        var passwords = new Mock<IPasswordService>(MockBehavior.Strict);
        var masking = new Mock<ISensitiveDataMaskingService>(MockBehavior.Strict);
        var sut = new UserManagementService(
            users.Object,
            passwords.Object,
            new TurkishIdentityNumberValidator(),
            masking.Object);

        var model = new CreateStaffUserViewModel
        {
            TcNo = "12345678901",
            Email = "a@b.com",
            FirstName = "Ad",
            LastName = "Soyad",
            RoleId = DomainConstants.RoleIds.ApplicationManager,
            Password = "Password1",
            ConfirmPassword = "Password1"
        };

        var result = await sut.CreateStaffUserAsync(model, Guid.NewGuid(), null, null);

        Assert.False(result.Success);
        Assert.Equal(TurkishIdentityNumber.InvalidMessage, result.ErrorMessage);
        Assert.DoesNotContain("12345678901", result.ErrorMessage ?? string.Empty, StringComparison.Ordinal);
        users.Verify(u => u.CreateStaffUserAtomicAsync(It.IsAny<CreateStaffUserRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static PhotoUploadRequest ValidPhoto() => new()
    {
        Content = new MemoryStream(JpegHeader),
        FileName = "photo.jpg",
        ContentType = "image/jpeg",
        Length = JpegHeader.Length
    };
}
