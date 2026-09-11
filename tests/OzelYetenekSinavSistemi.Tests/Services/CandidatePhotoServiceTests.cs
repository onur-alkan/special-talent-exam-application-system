using Moq;
using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.DTOs.Applications;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Application.Services;
using OzelYetenekSinavSistemi.Domain.Common;
using OzelYetenekSinavSistemi.Domain.Entities;

namespace OzelYetenekSinavSistemi.Tests.Services;

public sealed class CandidatePhotoServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();
    private static readonly Guid ApplicationId = Guid.NewGuid();
    private static readonly Guid ExamPeriodId = Guid.NewGuid();
    private static readonly Guid ManagerId = Guid.NewGuid();
    private const string PhotoPath = "/uploads/photos/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.jpg";

    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<ICandidateApplicationRepository> _apps = new();
    private readonly Mock<IExamPeriodManagerRepository> _managers = new();
    private readonly Mock<IPhotoUploadService> _photos = new();

    private CandidatePhotoService CreateSut() =>
        new(_users.Object, _apps.Object, _managers.Object, _photos.Object);

    private static PhotoFileContent SamplePhoto() => new()
    {
        Content = new byte[] { 0xFF, 0xD8, 0xFF },
        ContentType = "image/jpeg",
        FileName = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.jpg"
    };

    [Fact]
    public async Task GetCurrentUserPhoto_Succeeds()
    {
        _users.Setup(u => u.GetByIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = UserId, PhotoPath = PhotoPath });
        _photos.Setup(p => p.ReadPhotoAsync(PhotoPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<PhotoFileContent>.Ok(SamplePhoto()));

        var result = await CreateSut().GetCurrentUserPhotoAsync(UserId);

        Assert.True(result.Success);
        Assert.Equal("image/jpeg", result.Data!.ContentType);
    }

    [Fact]
    public async Task GetApplicationPhoto_CandidateOwn_Succeeds()
    {
        SetupApplication(UserId);
        _photos.Setup(p => p.ReadPhotoAsync(PhotoPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<PhotoFileContent>.Ok(SamplePhoto()));

        var result = await CreateSut().GetApplicationPhotoAsync(
            ApplicationId, UserId, DomainConstants.RoleNames.Candidate);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task GetApplicationPhoto_CandidateOther_FailsWithGenericMessage()
    {
        SetupApplication(UserId);

        var result = await CreateSut().GetApplicationPhotoAsync(
            ApplicationId, OtherUserId, DomainConstants.RoleNames.Candidate);

        Assert.False(result.Success);
        Assert.Equal("Fotoğraf bulunamadı.", result.ErrorMessage);
        _photos.Verify(p => p.ReadPhotoAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetApplicationPhoto_SuperAdmin_Succeeds()
    {
        SetupApplication(UserId);
        _photos.Setup(p => p.ReadPhotoAsync(PhotoPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<PhotoFileContent>.Ok(SamplePhoto()));

        var result = await CreateSut().GetApplicationPhotoAsync(
            ApplicationId, Guid.NewGuid(), DomainConstants.RoleNames.SuperAdmin);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task GetApplicationPhoto_AssignedManager_Succeeds()
    {
        SetupApplication(UserId);
        _managers.Setup(m => m.IsManagerOfExamPeriodAsync(ManagerId, ExamPeriodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _photos.Setup(p => p.ReadPhotoAsync(PhotoPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<PhotoFileContent>.Ok(SamplePhoto()));

        var result = await CreateSut().GetApplicationPhotoAsync(
            ApplicationId, ManagerId, DomainConstants.RoleNames.ApplicationManager);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task GetApplicationPhoto_UnassignedManager_Fails()
    {
        SetupApplication(UserId);
        _managers.Setup(m => m.IsManagerOfExamPeriodAsync(ManagerId, ExamPeriodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await CreateSut().GetApplicationPhotoAsync(
            ApplicationId, ManagerId, DomainConstants.RoleNames.ApplicationManager);

        Assert.False(result.Success);
        Assert.Equal("Fotoğraf bulunamadı.", result.ErrorMessage);
    }

    [Fact]
    public async Task GetApplicationPhoto_MissingApplication_FailsGenerically()
    {
        _apps.Setup(a => a.GetDetailByIdAsync(ApplicationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CandidateApplicationDetail?)null);

        var missing = await CreateSut().GetApplicationPhotoAsync(
            ApplicationId, UserId, DomainConstants.RoleNames.Candidate);
        SetupApplication(UserId);
        var unauthorized = await CreateSut().GetApplicationPhotoAsync(
            ApplicationId, OtherUserId, DomainConstants.RoleNames.Candidate);

        Assert.False(missing.Success);
        Assert.False(unauthorized.Success);
        Assert.Equal(missing.ErrorMessage, unauthorized.ErrorMessage);
    }

    private void SetupApplication(Guid ownerUserId) =>
        _apps.Setup(a => a.GetDetailByIdAsync(ApplicationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CandidateApplicationDetail
            {
                ApplicationId = ApplicationId,
                UserId = ownerUserId,
                ExamPeriodId = ExamPeriodId,
                PhotoPath = PhotoPath,
                ExamTitle = "Sınav",
                FirstName = "Ali",
                LastName = "Veli",
                TcNo = "10000000146",
                VerificationCode = "ABCDEF0123456789ABCDEF0123456789"
            });
}
