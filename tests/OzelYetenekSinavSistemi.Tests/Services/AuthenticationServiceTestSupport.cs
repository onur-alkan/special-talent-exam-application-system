using Moq;
using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.Interfaces.Infrastructure;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Application.Services;
using OzelYetenekSinavSistemi.Domain.Entities;

namespace OzelYetenekSinavSistemi.Tests.Services;

internal static class AuthenticationServiceTestSupport
{
    public static AuthenticationService Create(
        Guid ygsYearId,
        out Mock<IUserRepository> users,
        out Mock<IPhotoUploadService> photos,
        Mock<IYgsYearRepository>? years = null,
        Mock<IPasswordService>? passwords = null,
        Mock<IAuditService>? audits = null,
        Mock<ISensitiveDataMaskingService>? masking = null,
        IKeyedAsyncLock? keyedLock = null,
        TimeProvider? timeProvider = null,
        IIdentityDocumentValidator? identityDocumentValidator = null)
    {
        users = new Mock<IUserRepository>();
        photos = new Mock<IPhotoUploadService>();
        years ??= new Mock<IYgsYearRepository>();
        passwords ??= new Mock<IPasswordService>();
        audits ??= new Mock<IAuditService>();
        masking ??= new Mock<ISensitiveDataMaskingService>();

        years.Setup(r => r.GetByIdAsync(ygsYearId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new YgsYear { Id = ygsYearId });
        users.Setup(r => r.IdentityExistsAsync(
                It.IsAny<Domain.Enums.IdentityDocumentType>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        users.Setup(r => r.TcNoExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        users.Setup(r => r.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        photos.Setup(p => p.SavePhotoAsync(It.IsAny<PhotoUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PhotoUploadResult.Ok($"/uploads/photos/{Guid.NewGuid():N}.jpg"));
        passwords.Setup(p => p.Hash(It.IsAny<string>())).Returns("hash");
        audits.Setup(a => a.LogAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        masking.Setup(m => m.MaskTcNo(It.IsAny<string>())).Returns("***********");
        masking.Setup(m => m.MaskIdentityNumber(
                It.IsAny<Domain.Enums.IdentityDocumentType?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>()))
            .Returns("***********");
        masking.Setup(m => m.MaskEmail(It.IsAny<string>())).Returns("***@example.com");

        return new AuthenticationService(
            users.Object,
            years.Object,
            passwords.Object,
            identityDocumentValidator ?? new IdentityDocumentValidator(timeProvider ?? TimeProvider.System),
            photos.Object,
            audits.Object,
            masking.Object,
            keyedLock ?? PhotoUploadTestSupport.SharedLock,
            timeProvider ?? TimeProvider.System);
    }
}
