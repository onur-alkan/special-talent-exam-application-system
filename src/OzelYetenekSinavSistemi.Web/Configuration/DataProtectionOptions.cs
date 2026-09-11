using System.Runtime.Versioning;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace OzelYetenekSinavSistemi.Web.Configuration;

/// <summary>
/// Data Protection key ring yapılandırması. Secret veya sunucu yolu içermez.
/// </summary>
public sealed class DataProtectionOptions
{
    public const string SectionName = "DataProtection";
    public const string DefaultApplicationName = "OzelYetenekSinavSistemi";
    public const string DevelopmentRelativeKeysPath = "App_Data/DataProtection-Keys";

    /// <summary>Ortamlar arasında aynı izolasyon adı (cookie/token purpose ayırımı).</summary>
    public string ApplicationName { get; set; } = DefaultApplicationName;

    /// <summary>
    /// Key ring dizininin mutlak veya göreli yolu. Production'da zorunlu;
    /// Development'ta boşsa ContentRoot altındaki <see cref="DevelopmentRelativeKeysPath"/> kullanılır.
    /// </summary>
    public string? KeysPath { get; set; }

    /// <summary>
    /// true ise Windows'ta key ring dosyaları DPAPI ile şifrelenir.
    /// Development ve Windows dışı ortamlarda DPAPI uygulanmaz.
    /// </summary>
    public bool ProtectKeysAtRest { get; set; }

    /// <summary>
    /// ProtectKeysAtRest açıkken LocalMachine DPAPI kullanır (IIS app-pool kimliği etkiler).
    /// false (varsayılan): CurrentUser kapsamı.
    /// </summary>
    public bool ProtectToLocalMachine { get; set; }
}

public sealed class DataProtectionOptionsValidator : IValidateOptions<DataProtectionOptions>
{
    public ValidateOptionsResult Validate(string? name, DataProtectionOptions options)
    {
        if (options is null)
            return ValidateOptionsResult.Fail("DataProtection bölümü okunamadı.");

        if (string.IsNullOrWhiteSpace(options.ApplicationName))
            return ValidateOptionsResult.Fail("DataProtection:ApplicationName zorunludur.");

        if (options.ProtectToLocalMachine && !options.ProtectKeysAtRest)
        {
            return ValidateOptionsResult.Fail(
                "DataProtection:ProtectToLocalMachine yalnızca ProtectKeysAtRest=true iken kullanılabilir.");
        }

        return ValidateOptionsResult.Success;
    }
}

/// <summary>
/// Key ring yolu çözümleme, webroot koruması ve AddDataProtection kurulumu.
/// </summary>
public static class DataProtectionKeyRing
{
    public static string ResolveApplicationName(DataProtectionOptions options)
    {
        var name = options.ApplicationName?.Trim();
        return string.IsNullOrWhiteSpace(name)
            ? DataProtectionOptions.DefaultApplicationName
            : name;
    }

    /// <summary>
    /// Production'da KeysPath zorunlu; Development'ta boşsa yerel App_Data fallback.
    /// Fiziksel yolu kullanıcıya/HTTP'ye sızdıran mesaj üretmez.
    /// </summary>
    public static string ResolveKeysDirectory(IHostEnvironment environment, DataProtectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(options);

        string resolved;
        if (!string.IsNullOrWhiteSpace(options.KeysPath))
        {
            resolved = Path.GetFullPath(ExpandPath(options.KeysPath.Trim(), environment.ContentRootPath));
        }
        else if (environment.IsDevelopment())
        {
            resolved = Path.GetFullPath(
                Path.Combine(environment.ContentRootPath, DataProtectionOptions.DevelopmentRelativeKeysPath));
        }
        else
        {
            throw new InvalidOperationException(
                "DataProtection:KeysPath üretim ortamında zorunludur. " +
                "Anahtar dizinini ortam değişkeni veya deployment yapılandırması ile sağlayın.");
        }

        EnsureOutsideWebRoot(environment, resolved);
        return resolved;
    }

    public static void EnsureKeysDirectory(string keysDirectory)
    {
        if (string.IsNullOrWhiteSpace(keysDirectory))
            throw new InvalidOperationException("DataProtection anahtar dizini geçersiz.");

        Directory.CreateDirectory(keysDirectory);
    }

    public static void EnsureOutsideWebRoot(IHostEnvironment environment, string keysDirectory)
    {
        var webRoot = ResolveWebRoot(environment);
        var webFull = AppendDirectorySeparator(Path.GetFullPath(webRoot));
        var keysFull = AppendDirectorySeparator(Path.GetFullPath(keysDirectory));

        if (keysFull.StartsWith(webFull, StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                Path.GetFullPath(keysDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(webRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "DataProtection:KeysPath web kökünün (wwwroot) altında olamaz.");
        }
    }

    /// <summary>
    /// Production fail-fast + dizin hazırlığı. Geçici in-memory key ring'e düşmez.
    /// </summary>
    public static string ValidatePrepareAndResolve(
        IHostEnvironment environment,
        DataProtectionOptions options)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(options.ApplicationName))
            errors.Add("DataProtection:ApplicationName zorunludur.");

        if (options.ProtectToLocalMachine && !options.ProtectKeysAtRest)
            errors.Add("DataProtection:ProtectToLocalMachine yalnızca ProtectKeysAtRest=true iken kullanılabilir.");

        if (options.ProtectKeysAtRest
            && environment.IsProduction()
            && !OperatingSystem.IsWindows())
        {
            errors.Add(
                "DataProtection:ProtectKeysAtRest üretimde yalnızca Windows ortamında desteklenir.");
        }

        string? keysDirectory = null;
        try
        {
            keysDirectory = ResolveKeysDirectory(environment, options);
        }
        catch (InvalidOperationException ex)
        {
            errors.Add(ex.Message);
        }

        if (errors.Count > 0)
            throw new InvalidOperationException(string.Join(" ", errors));

        EnsureKeysDirectory(keysDirectory!);
        return keysDirectory!;
    }

    public static IDataProtectionBuilder AddConfiguredDataProtection(
        IServiceCollection services,
        IHostEnvironment environment,
        DataProtectionOptions options)
    {
        var keysDirectory = ValidatePrepareAndResolve(environment, options);
        var applicationName = ResolveApplicationName(options);

        var builder = services.AddDataProtection()
            .SetApplicationName(applicationName)
            .PersistKeysToFileSystem(new DirectoryInfo(keysDirectory));

        ApplyAtRestProtection(builder, environment, options);
        return builder;
    }

    public static void ApplyAtRestProtection(
        IDataProtectionBuilder builder,
        IHostEnvironment environment,
        DataProtectionOptions options)
    {
        if (!options.ProtectKeysAtRest)
            return;

        // Development ve Windows dışı: DPAPI çağrılmaz.
        if (environment.IsDevelopment() || !OperatingSystem.IsWindows())
            return;

        ApplyDpapi(builder, options.ProtectToLocalMachine);
    }

    [SupportedOSPlatform("windows")]
    private static void ApplyDpapi(IDataProtectionBuilder builder, bool protectToLocalMachine)
    {
        builder.ProtectKeysWithDpapi(protectToLocalMachine);
    }

    private static string ResolveWebRoot(IHostEnvironment environment)
    {
        if (environment is IWebHostEnvironment webEnv
            && !string.IsNullOrWhiteSpace(webEnv.WebRootPath))
        {
            return webEnv.WebRootPath;
        }

        return Path.Combine(environment.ContentRootPath, "wwwroot");
    }

    private static string ExpandPath(string path, string contentRoot)
    {
        if (Path.IsPathRooted(path))
            return path;

        return Path.Combine(contentRoot, path);
    }

    private static string AppendDirectorySeparator(string path)
    {
        if (path.EndsWith(Path.DirectorySeparatorChar)
            || path.EndsWith(Path.AltDirectorySeparatorChar))
        {
            return path;
        }

        return path + Path.DirectorySeparatorChar;
    }
}
