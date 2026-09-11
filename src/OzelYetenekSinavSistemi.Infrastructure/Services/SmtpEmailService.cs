using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Infrastructure.Configuration;

namespace OzelYetenekSinavSistemi.Infrastructure.Services;

/// <summary>
/// Production SMTP gönderimi (MailKit). TLS zorunlu; sertifika doğrulaması varsayılan güvenli.
/// Body/token/credentials loglanmaz.
/// </summary>
public sealed class SmtpEmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IOptions<EmailOptions> options, ILogger<SmtpEmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        EmailHeaderGuard.ValidateMessage(message);
        EmailHeaderGuard.ValidateSender(_options.FromAddress, _options.FromName);

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(_options.FromName.Trim(), _options.FromAddress.Trim()));
        mime.To.Add(MailboxAddress.Parse(message.To.Trim()));
        mime.Subject = message.Subject.Trim();

        var builder = new BodyBuilder
        {
            HtmlBody = message.HtmlBody,
            TextBody = message.PlainTextBody
        };
        mime.Body = builder.ToMessageBody();

        var secureSocket = _options.SecurityMode switch
        {
            EmailSecurityMode.SslOnConnect => SecureSocketOptions.SslOnConnect,
            EmailSecurityMode.StartTls => SecureSocketOptions.StartTls,
            _ => throw new InvalidOperationException("Email:SecurityMode TLS zorunludur.")
        };

        // Plaintext veya otomatik TLS düşürme kullanılmaz.
        using var client = new SmtpClient();
        // Sertifika doğrulaması kapatılmaz; varsayılan güvenli doğrulama kullanılır.
        client.Timeout = Math.Max(_options.ConnectionTimeoutSeconds, _options.SendTimeoutSeconds) * 1000;

        try
        {
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectCts.CancelAfter(TimeSpan.FromSeconds(_options.ConnectionTimeoutSeconds));

            await client.ConnectAsync(_options.Host.Trim(), _options.Port, secureSocket, connectCts.Token)
                .ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(_options.Username))
            {
                await client.AuthenticateAsync(
                        _options.Username.Trim(),
                        _options.Password,
                        connectCts.Token)
                    .ConfigureAwait(false);
            }

            using var sendCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            sendCts.CancelAfter(TimeSpan.FromSeconds(_options.SendTimeoutSeconds));

            await client.SendAsync(mime, sendCts.Token).ConfigureAwait(false);
            _logger.LogInformation("SmtpEmailSent");
        }
        finally
        {
            if (client.IsConnected)
            {
                try
                {
                    await client.DisconnectAsync(true, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "SmtpDisconnect");
                }
            }
        }
    }
}

/// <summary>Disabled modunda gönderim denemelerini açıkça reddeder.</summary>
public sealed class DisabledEmailService : IEmailService
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException(
            "Email:DeliveryMode=Disabled. Gönderim için File (Development) veya Smtp yapılandırın.");
}

internal static class EmailHeaderGuard
{
    public static void ValidateMessage(EmailMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.To) || !EmailOptionsValidator.IsValidEmail(message.To))
            throw new ArgumentException("Geçersiz alıcı e-posta adresi.", nameof(message));

        if (string.IsNullOrWhiteSpace(message.Subject) || EmailOptionsValidator.ContainsHeaderInjection(message.Subject))
            throw new ArgumentException("Geçersiz e-posta konusu.", nameof(message));

        if (string.IsNullOrWhiteSpace(message.HtmlBody) || string.IsNullOrWhiteSpace(message.PlainTextBody))
            throw new ArgumentException("E-posta gövdesi zorunludur.", nameof(message));
    }

    public static void ValidateSender(string fromAddress, string fromName)
    {
        if (!EmailOptionsValidator.IsValidEmail(fromAddress))
            throw new InvalidOperationException("Email:FromAddress geçersiz.");

        if (string.IsNullOrWhiteSpace(fromName) || EmailOptionsValidator.ContainsHeaderInjection(fromName))
            throw new InvalidOperationException("Email:FromName geçersiz.");
    }
}
