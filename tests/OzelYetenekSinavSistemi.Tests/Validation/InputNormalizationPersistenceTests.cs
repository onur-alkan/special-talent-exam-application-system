using Moq;
using OzelYetenekSinavSistemi.Application.DTOs.Users;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Application.Services;
using OzelYetenekSinavSistemi.Application.Validation;
using OzelYetenekSinavSistemi.Application.ViewModels.UserManagement;
using OzelYetenekSinavSistemi.Domain.Common;

namespace OzelYetenekSinavSistemi.Tests.Validation;

public sealed class InputNormalizationPersistenceTests
{
    [Fact]
    public void NormalizeHumanName_ProducesExpectedPersistenceValue()
    {
        Assert.Equal("Ömer Faruk", InputTextRules.NormalizeHumanName("  Ömer   Faruk  "));
        Assert.Equal("Jean-Pierre", InputTextRules.NormalizeHumanName("  Jean-Pierre  "));
    }

    [Fact]
    public void NormalizeMeaningfulTitle_CollapsesWhitespace()
    {
        Assert.True(InputTextRules.IsValidMeaningfulTitle("  2026   Özel Yetenek Sınavı  ", out var normalized));
        Assert.Equal("2026 Özel Yetenek Sınavı", normalized);
    }

    [Fact]
    public async Task UserManagementService_PersistsNormalizedStaffNames()
    {
        CreateStaffUserRequest? captured = null;
        var userRepo = new Mock<IUserRepository>();
        userRepo.Setup(r => r.TcNoExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        userRepo.Setup(r => r.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        userRepo.Setup(r => r.CreateStaffUserAtomicAsync(It.IsAny<CreateStaffUserRequest>(), It.IsAny<CancellationToken>()))
            .Callback<CreateStaffUserRequest, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(UserManagementWriteResult.Ok(Guid.NewGuid()));

        var sut = new UserManagementService(
            userRepo.Object,
            Mock.Of<IPasswordService>(s => s.Hash(It.IsAny<string>()) == "hash"),
            new TurkishIdentityNumberValidator(),
            Mock.Of<ISensitiveDataMaskingService>());

        var result = await sut.CreateStaffUserAsync(
            new CreateStaffUserViewModel
            {
                TcNo = "10000000146",
                Email = "staff@example.com",
                FirstName = "  Ömer   Faruk  ",
                LastName = "  Yılmaz  ",
                RoleId = DomainConstants.RoleIds.ApplicationManager,
                Password = "Passw0rd!",
                ConfirmPassword = "Passw0rd!"
            },
            Guid.NewGuid(),
            null,
            null,
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(captured);
        Assert.Equal("Ömer Faruk", captured!.FirstName);
        Assert.Equal("Yılmaz", captured.LastName);
    }

    [Fact]
    public void PasswordFields_AreNotNormalizedByHumanNameRules()
    {
        var password = "  Pass w0rd!  ";
        Assert.False(InputTextRules.IsValidHumanName(password, out _));
        Assert.Equal("  Pass w0rd!  ", password);
    }
}
