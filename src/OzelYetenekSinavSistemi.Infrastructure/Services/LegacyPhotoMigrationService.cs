using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OzelYetenekSinavSistemi.Infrastructure.Configuration;

namespace OzelYetenekSinavSistemi.Infrastructure.Services;

/// <summary>
/// wwwroot/uploads/photos altındaki legacy GUID fotoğrafları private storage'a taşır.
/// Idempotent; geçersiz dosya adlarına dokunmaz; hedef çakışmasında üzerine yazmaz.
/// </summary>
public sealed class LegacyPhotoMigrationService
{
    private static readonly Regex GeneratedPhotoFileName =
        new(@"^[0-9a-fA-F]{32}\.(jpg|png)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly PhotoUploadOptions _options;
    private readonly ILogger<LegacyPhotoMigrationService> _logger;

    public LegacyPhotoMigrationService(
        IOptions<PhotoUploadOptions> options,
        ILogger<LegacyPhotoMigrationService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(_options.WebRootPath) || string.IsNullOrWhiteSpace(_options.StorageRootPath))
            return Task.CompletedTask;

        var legacyFolder = Path.GetFullPath(Path.Combine(_options.WebRootPath, "uploads", "photos"));
        var privateFolder = Path.GetFullPath(_options.StorageRootPath);

        if (!Directory.Exists(legacyFolder))
            return Task.CompletedTask;

        Directory.CreateDirectory(privateFolder);

        foreach (var sourcePath in Directory.EnumerateFiles(legacyFolder))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileName = Path.GetFileName(sourcePath);
            if (!GeneratedPhotoFileName.IsMatch(fileName))
            {
                _logger.LogWarning("Legacy fotoğraf migration: geçersiz dosya adı atlandı.");
                continue;
            }

            var destinationPath = Path.GetFullPath(Path.Combine(privateFolder, fileName));
            var privateRoot = Path.GetFullPath(privateFolder + Path.DirectorySeparatorChar);
            if (!destinationPath.StartsWith(privateRoot, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Legacy fotoğraf migration: hedef yol private klasör dışında.");
                continue;
            }

            try
            {
                if (File.Exists(destinationPath))
                {
                    if (FilesHaveSameContent(sourcePath, destinationPath))
                    {
                        File.Delete(sourcePath);
                    }
                    else
                    {
                        _logger.LogWarning("Legacy fotoğraf migration: hedefte farklı içerikli dosya mevcut; üzerine yazılmadı.");
                    }

                    continue;
                }

                File.Move(sourcePath, destinationPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Legacy fotoğraf migration sırasında bir dosya taşınamadı.");
            }
        }

        return Task.CompletedTask;
    }

    private static bool FilesHaveSameContent(string leftPath, string rightPath)
    {
        var leftInfo = new FileInfo(leftPath);
        var rightInfo = new FileInfo(rightPath);
        if (leftInfo.Length != rightInfo.Length)
            return false;

        var leftHash = SHA256.HashData(File.ReadAllBytes(leftPath));
        var rightHash = SHA256.HashData(File.ReadAllBytes(rightPath));
        return CryptographicOperations.FixedTimeEquals(leftHash, rightHash);
    }
}
