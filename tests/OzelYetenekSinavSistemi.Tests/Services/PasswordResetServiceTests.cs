using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Application.Services;
using OzelYetenekSinavSistemi.Application.ViewModels.Account;
using OzelYetenekSinavSistemi.Domain.Entities;

namespace OzelYetenekSinavSistemi.Tests.Services;

public sealed class PasswordResetServiceTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IPasswordResetTokenRepository> _tokenRepo = new();
    private readonly Mock<IPasswordResetTransactionService> _transaction = new();
    private readonly Mock<IPasswordService> _passwordService = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<IAuditService> _audit = new();

    private PasswordResetService CreateSut() =>
        new(_userRepo.Object, _tokenRepo.Object, _transaction.Object, _passwordService.Object,
            _emailService.Object, new PasswordResetEmailTemplate(), _audit.Object,
            NullLogger<PasswordResetService>.Instance);

    private static ResetPasswordViewModel Model(string token = "plain-token") => new()
    {
        Token = token,
        NewPassword = "NewPass!23",
        ConfirmPassword = "NewPass!23"
    };

    [Fact]
    public async Task ResetPassword_EmptyToken_FailsWithoutTransaction()
    {
        var result = await CreateSut().ResetPasswordAsync(Model(token: " "), null);

        Assert.False(result.Success);
        _transaction.Verify(
            t => t.ConsumeTokenAndUpdatePasswordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _audit.Verify(
            a => a.LogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ResetPassword_ExpiredToken_FailsWithoutSuccessAudit()
    {
        _passwordService.Setup(p => p.Hash(It.IsAny<string>())).Returns("hashed");
        _transaction.Setup(t => t.ConsumeTokenAndUpdatePasswordAsync(It.IsAny<string>(), "hashed", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PasswordResetConsumeResult.Failed(PasswordResetConsumeStatus.TokenExpired));

        var result = await CreateSut().ResetPasswordAsync(Model(), null);

        Assert.False(result.Success);
        Assert.Contains("süresi dolmuş", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        VerifyNoCompletedAudit();
    }

    [Fact]
    public async Task ResetPassword_UsedToken_FailsWithoutSuccessAudit()
    {
        _passwordService.Setup(p => p.Hash(It.IsAny<string>())).Returns("hashed");
        _transaction.Setup(t => t.ConsumeTokenAndUpdatePasswordAsync(It.IsAny<string>(), "hashed", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PasswordResetConsumeResult.Failed(PasswordResetConsumeStatus.TokenAlreadyUsed));

        var result = await CreateSut().ResetPasswordAsync(Model(), null);

        Assert.False(result.Success);
        Assert.Contains("kullanılmış", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        VerifyNoCompletedAudit();
    }

    [Fact]
    public async Task ResetPassword_UnknownToken_FailsWithoutSuccessAudit()
    {
        _passwordService.Setup(p => p.Hash(It.IsAny<string>())).Returns("hashed");
        _transaction.Setup(t => t.ConsumeTokenAndUpdatePasswordAsync(It.IsAny<string>(), "hashed", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PasswordResetConsumeResult.Failed(PasswordResetConsumeStatus.TokenNotFound));

        var result = await CreateSut().ResetPasswordAsync(Model(), null);

        Assert.False(result.Success);
        VerifyNoCompletedAudit();
    }

    [Fact]
    public async Task ResetPassword_ValidToken_SucceedsAndWritesAuditAfterCommit()
    {
        var userId = Guid.NewGuid();
        _passwordService.Setup(p => p.Hash(It.IsAny<string>())).Returns("hashed");
        _transaction.Setup(t => t.ConsumeTokenAndUpdatePasswordAsync(It.IsAny<string>(), "hashed", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PasswordResetConsumeResult.Succeeded(userId));

        var result = await CreateSut().ResetPasswordAsync(Model(), "127.0.0.1");

        Assert.True(result.Success);
        _userRepo.Verify(r => r.UpdatePasswordAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        _tokenRepo.Verify(r => r.MarkAsUsedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _audit.Verify(
            a => a.LogAsync("PasswordResetCompleted", It.IsAny<string>(), userId, "127.0.0.1", null, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ResetPassword_TokenUpdateFailed_DoesNotWriteSuccessAudit()
    {
        _passwordService.Setup(p => p.Hash(It.IsAny<string>())).Returns("hashed");
        _transaction.Setup(t => t.ConsumeTokenAndUpdatePasswordAsync(It.IsAny<string>(), "hashed", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PasswordResetConsumeResult.Failed(PasswordResetConsumeStatus.TokenUpdateFailed));

        var result = await CreateSut().ResetPasswordAsync(Model(), null);

        Assert.False(result.Success);
        VerifyNoCompletedAudit();
    }

    [Fact]
    public async Task RequestReset_UnknownUser_ReturnsGenericSuccessAndSendsNoEmail()
    {
        _userRepo.Setup(r => r.GetByTcNoOrEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await CreateSut().RequestResetAsync("unknown@example.com", "https://localhost/Account/ResetPassword", null);

        Assert.True(result.Success);
        _emailService.Verify(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private void VerifyNoCompletedAudit()
    {
        _audit.Verify(
            a => a.LogAsync("PasswordResetCompleted", It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
