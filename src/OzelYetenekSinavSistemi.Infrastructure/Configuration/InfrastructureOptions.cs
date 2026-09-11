using System.Net.Mail;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OzelYetenekSinavSistemi.Application.Common;

namespace OzelYetenekSinavSistemi.Infrastructure.Configuration;

public enum EmailDeliveryMode
{
    Disabled = 0,
    File = 1,
    Smtp = 2
}

public enum EmailSecurityMode
{
    StartTls = 0,
    SslOnConnect = 1
}

public sealed class PhotoUploadOptions
{
    public const string SectionName = "PhotoUpload";

    /// <summary>
    /// Legacy wwwroot yolu (yalnızca eski dosya migration için).
    /// Yeni kayıtlar bu yolu kullanmaz.
    /// </summary>
    public string WebRootPath { get; set; } = string.Empty;

    /// <summary>
    /// Private fotoğraf depolama klasörünün mutlak yolu
    /// (ContentRoot/App_Data/private-uploads/photos). Kaynak appsettings'e yazılmaz.
    /// </summary>
    public string StorageRootPath { get; set; } = string.Empty;

    /// <summary>Dosya boyutu üst sınırı. Varsayılan ve tavan: <see cref="PhotoUploadLimits.MaxFileBytes"/>.</summary>
    public long MaxBytes { get; set; } = PhotoUploadLimits.MaxFileBytes;
}

/// <summary>PhotoUploadOptions startup doğrulaması.</summary>
public sealed class PhotoUploadOptionsValidator : IValidateOptions<PhotoUploadOptions>
{
    public ValidateOptionsResult Validate(string? name, PhotoUploadOptions options)
    {
        var errors = new List<string>();
        ValidateCore(options, errors);
        return errors.Count > 0 ? ValidateOptionsResult.Fail(errors) : ValidateOptionsResult.Success;
    }

    public static void ValidateCore(PhotoUploadOptions options, List<string> errors)
    {
        if (options.MaxBytes < PhotoUploadLimits.MinConfigurableFileBytes)
        {
            errors.Add(
                $"PhotoUpload:MaxBytes en az {PhotoUploadLimits.MinConfigurableFileBytes} bayt olmalıdır.");
        }

        if (options.MaxBytes > PhotoUploadLimits.MaxFileBytes)
        {
            errors.Add(
                $"PhotoUpload:MaxBytes en fazla {PhotoUploadLimits.MaxFileBytes} bayt olabilir (güvenlik tavanı).");
        }

        // StorageRootPath boş olabilir: DI PostConfigure ContentRoot App_Data yolunu doldurur.
        // wwwroot kontrolü yalnız her iki yol da set iken yapılır.
        if (string.IsNullOrWhiteSpace(options.StorageRootPath)
            || string.IsNullOrWhiteSpace(options.WebRootPath))
            return;

        try
        {
            var storage = Path.GetFullPath(options.StorageRootPath);
            var webRoot = Path.GetFullPath(options.WebRootPath);
            var webPrefix = webRoot.EndsWith(Path.DirectorySeparatorChar)
                || webRoot.EndsWith(Path.AltDirectorySeparatorChar)
                ? webRoot
                : webRoot + Path.DirectorySeparatorChar;

            if (storage.StartsWith(webPrefix, StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    storage.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    webRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("PhotoUpload storage dizini web kökünün (wwwroot) altında olamaz.");
            }
        }
        catch (Exception)
        {
            errors.Add("PhotoUpload storage dizini geçersiz.");
        }
    }
}

public sealed class EmailOptions
{
    public const string SectionName = "Email";
    public const int MinTimeoutSeconds = 5;
    public const int MaxTimeoutSeconds = 120;
    public const int MinPort = 1;
    public const int MaxPort = 65535;

    public EmailDeliveryMode DeliveryMode { get; set; } = EmailDeliveryMode.Disabled;

    /// <summary>Development File modunda e-postaların yazılacağı klasör (ContentRoot'a göre).</summary>
    public string DevPickupDirectory { get; set; } = "App_Data/dev-emails";

    public string FromAddress { get; set; } = "no-reply@example.com";
    public string FromName { get; set; } = "Özel Yetenek Sınavları Başvuru Sistemi";

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public EmailSecurityMode SecurityMode { get; set; } = EmailSecurityMode.StartTls;
    public int ConnectionTimeoutSeconds { get; set; } = 15;
    public int SendTimeoutSeconds { get; set; } = 30;
}

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public string SuperAdminTcNo { get; set; } = "11111111110";
    public string SuperAdminEmail { get; set; } = "admin@example.com";
    public string SuperAdminFirstName { get; set; } = "Sistem";
    public string SuperAdminLastName { get; set; } = "Yöneticisi";

    /// <summary>
    /// İlk SuperAdmin oluşturulurken kullanılır. Kaynak kodda veya appsettings'te tutulmamalı;
    /// User Secrets / ortam değişkeni ile sağlanmalıdır.
    /// </summary>
    public string SuperAdminPassword { get; set; } = string.Empty;

    /// <summary>Development ortamında örnek sınav dönemi seed'ini açar. Production'da false kalmalıdır.</summary>
    public bool SeedTestData { get; set; } = false;
}

/// <summary>Production'da SeedTestData=true fail-fast.</summary>
public sealed class SeedOptionsValidator : IValidateOptions<SeedOptions>
{
    private readonly IHostEnvironment _environment;

    public SeedOptionsValidator(IHostEnvironment environment) => _environment = environment;

    public ValidateOptionsResult Validate(string? name, SeedOptions options)
    {
        if (_environment.IsProduction() && options.SeedTestData)
        {
            return ValidateOptionsResult.Fail(
                "Production ortamında Seed:SeedTestData true olamaz.");
        }

        return ValidateOptionsResult.Success;
    }
}

/// <summary>EmailOptions startup doğrulaması. Secret veya connection string loglamaz.</summary>
public sealed class EmailOptionsValidator : IValidateOptions<EmailOptions>
{
    private readonly IHostEnvironment _environment;

    public EmailOptionsValidator(IHostEnvironment environment) => _environment = environment;

    public ValidateOptionsResult Validate(string? name, EmailOptions options)
    {
        var errors = new List<string>();
        ValidateCore(options, _environment.IsProduction(), errors);
        return errors.Count > 0 ? ValidateOptionsResult.Fail(errors) : ValidateOptionsResult.Success;
    }

    public static void ValidateCore(EmailOptions options, bool isProduction, List<string> errors)
    {
        if (!Enum.IsDefined(typeof(EmailDeliveryMode), options.DeliveryMode))
        {
            errors.Add("Email:DeliveryMode geçersiz.");
            return;
        }

        ValidateCommonSender(options, errors);

        if (options.ConnectionTimeoutSeconds < EmailOptions.MinTimeoutSeconds
            || options.ConnectionTimeoutSeconds > EmailOptions.MaxTimeoutSeconds)
        {
            errors.Add(
                $"Email:ConnectionTimeoutSeconds {EmailOptions.MinTimeoutSeconds}-{EmailOptions.MaxTimeoutSeconds} aralığında olmalıdır.");
        }

        if (options.SendTimeoutSeconds < EmailOptions.MinTimeoutSeconds
            || options.SendTimeoutSeconds > EmailOptions.MaxTimeoutSeconds)
        {
            errors.Add(
                $"Email:SendTimeoutSeconds {EmailOptions.MinTimeoutSeconds}-{EmailOptions.MaxTimeoutSeconds} aralığında olmalıdır.");
        }

        if (isProduction)
        {
            if (options.DeliveryMode != EmailDeliveryMode.Smtp)
            {
                errors.Add("Production ortamında Email:DeliveryMode Smtp olmalıdır (File/Disabled kabul edilmez).");
                return;
            }

            ValidateSmtp(options, requireTls: true, errors);
            return;
        }

        switch (options.DeliveryMode)
        {
            case EmailDeliveryMode.Disabled:
                break;
            case EmailDeliveryMode.File:
                if (string.IsNullOrWhiteSpace(options.DevPickupDirectory))
                    errors.Add("Email:DevPickupDirectory File modunda zorunludur.");
                else if (ContainsTraversal(options.DevPickupDirectory))
                    errors.Add("Email:DevPickupDirectory geçersiz yol içeriyor.");
                break;
            case EmailDeliveryMode.Smtp:
                ValidateSmtp(options, requireTls: true, errors);
                break;
        }
    }

    private static void ValidateCommonSender(EmailOptions options, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(options.FromAddress) || !IsValidEmail(options.FromAddress))
            errors.Add("Email:FromAddress geçerli bir e-posta adresi olmalıdır.");

        if (string.IsNullOrWhiteSpace(options.FromName))
            errors.Add("Email:FromName zorunludur.");
        else if (ContainsHeaderInjection(options.FromName))
            errors.Add("Email:FromName CR/LF içeremez.");
    }

    private static void ValidateSmtp(EmailOptions options, bool requireTls, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(options.Host))
            errors.Add("Email:Host Smtp modunda zorunludur.");
        else if (ContainsHeaderInjection(options.Host))
            errors.Add("Email:Host geçersiz karakter içeriyor.");

        if (options.Port < EmailOptions.MinPort || options.Port > EmailOptions.MaxPort)
            errors.Add($"Email:Port {EmailOptions.MinPort}-{EmailOptions.MaxPort} aralığında olmalıdır.");

        if (!Enum.IsDefined(typeof(EmailSecurityMode), options.SecurityMode))
            errors.Add("Email:SecurityMode StartTls veya SslOnConnect olmalıdır.");

        if (requireTls
            && options.SecurityMode is not (EmailSecurityMode.StartTls or EmailSecurityMode.SslOnConnect))
        {
            errors.Add("Email:SecurityMode TLS zorunludur (StartTls veya SslOnConnect).");
        }

        var hasUser = !string.IsNullOrWhiteSpace(options.Username);
        var hasPass = !string.IsNullOrEmpty(options.Password);
        if (hasUser && !hasPass)
            errors.Add("Email:Username verildiğinde Email:Password zorunludur.");
        if (hasPass && ContainsHeaderInjection(options.Username ?? string.Empty))
            errors.Add("Email:Username geçersiz karakter içeriyor.");
    }

    public static bool IsValidEmail(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || ContainsHeaderInjection(value))
            return false;

        try
        {
            var addr = new MailAddress(value.Trim());
            return string.Equals(addr.Address, value.Trim(), StringComparison.OrdinalIgnoreCase)
                   || string.Equals(addr.Address, value.Trim(), StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    public static bool ContainsHeaderInjection(string value) =>
        value.IndexOfAny(['\r', '\n', '\0']) >= 0;

    private static bool ContainsTraversal(string path) =>
        path.Contains("..", StringComparison.Ordinal)
        || path.IndexOfAny(['\r', '\n', '\0']) >= 0;
}
