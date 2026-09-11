using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Application.ViewModels.Account;
using OzelYetenekSinavSistemi.Domain.Entities;

namespace OzelYetenekSinavSistemi.Application.Services;

public sealed class PasswordResetService : IPasswordResetService
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(2);

    private readonly IUserRepository _userRepository;
    private readonly IPasswordResetTokenRepository _tokenRepository;
    private readonly IPasswordResetTransactionService _passwordResetTransaction;
    private readonly IPasswordService _passwordService;
    private readonly IEmailService _emailService;
    private readonly IPasswordResetEmailTemplate _emailTemplate;
    private readonly IAuditService _auditService;
    private readonly ILogger<PasswordResetService> _logger;

    public PasswordResetService(
        IUserRepository userRepository,
        IPasswordResetTokenRepository tokenRepository,
        IPasswordResetTransactionService passwordResetTransaction,
        IPasswordService passwordService,
        IEmailService emailService,
        IPasswordResetEmailTemplate emailTemplate,
        IAuditService auditService,
        ILogger<PasswordResetService> logger)
    {
        _userRepository = userRepository;
        _tokenRepository = tokenRepository;
        _passwordResetTransaction = passwordResetTransaction;
        _passwordService = passwordService;
        _emailService = emailService;
        _emailTemplate = emailTemplate;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<OperationResult> RequestResetAsync(
        string tcNoOrEmail,
        string resetLinkBaseUrl,
        string? ipAddress,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        // Hesabın var olup olmadığını dışarıya belli etmemek için her durumda genel cevap döneriz.
        var user = await _userRepository.GetByTcNoOrEmailAsync(tcNoOrEmail.Trim(), cancellationToken);
        if (user is null || !user.IsActive)
            return OperationResult.Ok();

        await _tokenRepository.InvalidateActiveTokensAsync(user.Id, cancellationToken);

        var plainToken = GenerateSecureToken();
        var tokenHash = ComputeHash(plainToken);

        var entity = new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.Add(TokenLifetime),
            CreatedIpAddress = ipAddress
        };

        await _tokenRepository.AddAsync(entity, cancellationToken);

        var separator = resetLinkBaseUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        var resetLink = $"{resetLinkBaseUrl}{separator}token={Uri.EscapeDataString(plainToken)}";
        var message = _emailTemplate.Create(user.FirstName, user.Email, resetLink);

        try
        {
            // Düz token yalnızca e-posta içeriğine yazılır; loglara ASLA yazılmaz.
            await _emailService.SendAsync(message, cancellationToken);
        }
        catch (Exception)
        {
            // SMTP/gönderim hatası hesap varlığını açığa çıkarmamalı.
            // Oluşturulan token güvenli biçimde geçersizleştirilir (UsedAt set).
            try
            {
                await _tokenRepository.MarkAsUsedAsync(entity.Id, cancellationToken);
            }
            catch (Exception invalidateEx)
            {
                _logger.LogWarning(
                    invalidateEx,
                    "EmailDeliveryFailed sonrası token invalidasyonu başarısız. UserId={UserId}, CorrelationId={CorrelationId}",
                    user.Id,
                    correlationId);
            }

            await _auditService.LogAsync(
                "EmailDeliveryFailed",
                "PasswordResetSmtpSendFailed",
                user.Id,
                ipAddress,
                null,
                correlationId,
                cancellationToken);

            _logger.LogWarning(
                "EmailDeliveryFailed. Event=PasswordResetSmtpSendFailed UserId={UserId} CorrelationId={CorrelationId}",
                user.Id,
                correlationId);

            return OperationResult.Ok();
        }

        await _auditService.LogAsync(
            "PasswordResetRequested",
            "Parola sıfırlama talebi oluşturuldu.",
            user.Id,
            ipAddress,
            null,
            correlationId,
            cancellationToken);

        return OperationResult.Ok();
    }

    public async Task<OperationResult> ResetPasswordAsync(
        ResetPasswordViewModel model,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model.Token))
            return OperationResult.Fail("Geçersiz veya süresi dolmuş bağlantı.");

        var tokenHash = ComputeHash(model.Token);
        var newHash = _passwordService.Hash(model.NewPassword);

        var consumeResult = await _passwordResetTransaction
            .ConsumeTokenAndUpdatePasswordAsync(tokenHash, newHash, cancellationToken)
            .ConfigureAwait(false);

        if (!consumeResult.Success)
            return MapFailure(consumeResult.Status);

        await _auditService.LogAsync(
            "PasswordResetCompleted",
            "Parola sıfırlandı.",
            consumeResult.UserId,
            ipAddress,
            null,
            null,
            cancellationToken).ConfigureAwait(false);

        return OperationResult.Ok();
    }

    private static OperationResult MapFailure(PasswordResetConsumeStatus status) =>
        status switch
        {
            PasswordResetConsumeStatus.TokenAlreadyUsed =>
                OperationResult.Fail("Bu bağlantı daha önce kullanılmış."),
            PasswordResetConsumeStatus.TokenExpired =>
                OperationResult.Fail("Bağlantının süresi dolmuş. Yeniden bir talep oluşturunuz."),
            PasswordResetConsumeStatus.UserUpdateFailed =>
                OperationResult.Fail("Kullanıcı bulunamadı."),
            PasswordResetConsumeStatus.TokenUpdateFailed =>
                OperationResult.Fail("Geçersiz veya süresi dolmuş bağlantı."),
            _ =>
                OperationResult.Fail("Geçersiz veya süresi dolmuş bağlantı.")
        };

    private static string GenerateSecureToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", string.Empty);
    }

    private static string ComputeHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }
}
