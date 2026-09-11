using System.Net.Mail;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Infrastructure.Configuration;

namespace OzelYetenekSinavSistemi.Infrastructure.Services;

/// <summary>
/// Development File modu: e-postaları ContentRoot/App_Data/dev-emails altına yazar.
/// Production'da kaydedilmemeli / validation engeller.
/// </summary>
public sealed class DevFileEmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly string _contentRootPath;
    private readonly ILogger<DevFileEmailService> _logger;

    public DevFileEmailService(
        IOptions<EmailOptions> options,
        IHostEnvironment environment,
        ILogger<DevFileEmailService> logger)
    {
        _options = options.Value;
        _contentRootPath = environment.ContentRootPath;
        _logger = logger;

        if (environment.IsProduction())
            throw new InvalidOperationException("DevFileEmailService Production ortamında kullanılamaz.");
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        EmailHeaderGuard.ValidateMessage(message);

        var directory = ResolvePickupDirectory();
        Directory.CreateDirectory(directory);

        var fileName = $"{Guid.NewGuid():N}.eml.html";
        var fullPath = Path.GetFullPath(Path.Combine(directory, fileName));
        if (!fullPath.StartsWith(directory, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("E-posta dosya yolu güvenlik kontrolünden geçemedi.");

        var encodedTo = HtmlEncoder.Default.Encode(message.To);
        var encodedSubject = HtmlEncoder.Default.Encode(message.Subject);
        var encodedFromName = HtmlEncoder.Default.Encode(_options.FromName);
        var encodedFromAddress = HtmlEncoder.Default.Encode(_options.FromAddress);

        var content =
            $"<!-- To: {encodedTo} -->{Environment.NewLine}" +
            $"<!-- From: {encodedFromName} &lt;{encodedFromAddress}&gt; -->{Environment.NewLine}" +
            $"<!-- Subject: {encodedSubject} -->{Environment.NewLine}" +
            $"<!-- Date: {DateTime.UtcNow:O} -->{Environment.NewLine}" +
            "<html><head><meta charset=\"utf-8\" /></head><body>" +
            $"<h3>{encodedSubject}</h3>" +
            $"<p><strong>Kime:</strong> {encodedTo}</p><hr/>" +
            message.HtmlBody +
            "<hr/><pre style=\"white-space:pre-wrap;font-family:monospace;\">" +
            HtmlEncoder.Default.Encode(message.PlainTextBody) +
            "</pre></body></html>";

        await using (var stream = new FileStream(
                         fullPath,
                         FileMode.CreateNew,
                         FileAccess.Write,
                         FileShare.None,
                         4096,
                         FileOptions.Asynchronous | FileOptions.SequentialScan))
        await using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
        {
            await writer.WriteAsync(content).ConfigureAwait(false);
            await writer.FlushAsync().ConfigureAwait(false);
        }

        // Gövde/recipient/token loglanmaz; yalnızca rastgele dosya adı.
        _logger.LogInformation("Development e-postası dosyaya yazıldı: {EmailFile}", fileName);
    }

    private string ResolvePickupDirectory()
    {
        var configured = _options.DevPickupDirectory?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(configured) || configured.Contains("..", StringComparison.Ordinal))
            throw new InvalidOperationException("Email:DevPickupDirectory geçersiz.");

        var combined = Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(_contentRootPath, configured);

        var full = Path.GetFullPath(combined);
        var contentRoot = Path.GetFullPath(_contentRootPath);
        if (!full.StartsWith(contentRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("DevPickupDirectory ContentRoot dışında olamaz.");

        // wwwroot altına yazmayı engelle
        var webHint = Path.Combine(contentRoot, "wwwroot");
        if (full.StartsWith(Path.GetFullPath(webHint), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("DevPickupDirectory web root altında olamaz.");

        return full;
    }
}
