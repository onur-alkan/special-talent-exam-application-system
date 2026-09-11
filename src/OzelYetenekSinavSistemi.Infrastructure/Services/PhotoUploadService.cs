using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Infrastructure.Configuration;

namespace OzelYetenekSinavSistemi.Infrastructure.Services;

/// <summary>
/// Güvenli vesikalık fotoğraf yükleme: private storage, uzantı/MIME/imza/yapı,
/// GUID adlandırma, path traversal koruması ve gerçek boyut sınırı.
/// </summary>
public sealed class PhotoUploadService : IPhotoUploadService
{
    private static readonly Regex GeneratedPhotoFileName =
        new(@"^[0-9a-fA-F]{32}\.(jpg|png)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private const string LogicalFolder = "uploads/photos";
    private const int CopyBufferSize = 80 * 1024;
    private const string GenericReadFailure = "Fotoğraf bulunamadı.";

    private readonly PhotoUploadOptions _options;

    public PhotoUploadService(IOptions<PhotoUploadOptions> options)
    {
        _options = options.Value;
    }

    public async Task<PhotoUploadResult> ValidatePhotoAsync(
        PhotoUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        var outcome = await InspectAsync(request, persist: false, cancellationToken).ConfigureAwait(false);
        return outcome.Result;
    }

    public async Task<PhotoUploadResult> SavePhotoAsync(
        PhotoUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        var outcome = await InspectAsync(request, persist: true, cancellationToken).ConfigureAwait(false);
        return outcome.Result;
    }

    public Task<bool> DeletePhotoAsync(string? relativePhotoPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(relativePhotoPath))
            return Task.FromResult(true);

        if (!TryResolveSafePhysicalPath(relativePhotoPath, out var fullPath))
            return Task.FromResult(false);

        if (!File.Exists(fullPath))
            return Task.FromResult(true);

        File.Delete(fullPath);
        return Task.FromResult(true);
    }

    public async Task<OperationResult<PhotoFileContent>> ReadPhotoAsync(
        string? logicalPhotoPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryResolveSafePhysicalPath(logicalPhotoPath, out var fullPath))
            return OperationResult<PhotoFileContent>.Fail(GenericReadFailure);

        if (!File.Exists(fullPath))
            return OperationResult<PhotoFileContent>.Fail(GenericReadFailure);

        var fileName = Path.GetFileName(fullPath);
        var contentType = ResolveContentType(fileName);
        if (contentType is null)
            return OperationResult<PhotoFileContent>.Fail(GenericReadFailure);

        await using var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        await using var limited = new MemoryStream();
        var bytesRead = await CopyLimitedAsync(fileStream, limited, _options.MaxBytes + 1, cancellationToken)
            .ConfigureAwait(false);

        if (bytesRead <= 0 || bytesRead > _options.MaxBytes)
            return OperationResult<PhotoFileContent>.Fail(GenericReadFailure);

        return OperationResult<PhotoFileContent>.Ok(new PhotoFileContent
        {
            Content = limited.ToArray(),
            ContentType = contentType,
            FileName = fileName
        });
    }

    private async Task<InspectOutcome> InspectAsync(
        PhotoUploadRequest request,
        bool persist,
        CancellationToken cancellationToken)
    {
        if (request.Content is null)
            return Fail(PhotoUploadErrorCode.Missing);

        if (request.Length > _options.MaxBytes)
            return Fail(PhotoUploadErrorCode.TooLarge);

        var fileName = Path.GetFileName(request.FileName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(fileName))
            return Fail(PhotoUploadErrorCode.Missing);

        if (HasDisallowedCompoundExtension(fileName))
            return Fail(PhotoUploadErrorCode.InvalidFileName);

        var extension = Path.GetExtension(fileName);
        if (!PhotoUploadLimits.IsAllowedExtension(extension))
            return Fail(PhotoUploadErrorCode.UnsupportedExtension);

        if (!PhotoUploadLimits.IsAllowedContentType(request.ContentType))
            return Fail(PhotoUploadErrorCode.UnsupportedMimeType);

        if (persist && string.IsNullOrWhiteSpace(_options.StorageRootPath))
            return Fail(PhotoUploadErrorCode.StorageFailure);

        string? fullPath = null;
        try
        {
            await using var limitedContent = new MemoryStream();
            var bytesRead = await CopyLimitedAsync(request.Content, limitedContent, _options.MaxBytes + 1, cancellationToken)
                .ConfigureAwait(false);

            if (bytesRead <= 0)
                return Fail(PhotoUploadErrorCode.Empty);

            if (bytesRead > _options.MaxBytes)
                return Fail(PhotoUploadErrorCode.TooLarge);

            limitedContent.Position = 0;
            var bytes = limitedContent.ToArray();

            var detectedExtension = DetectImageType(bytes);
            if (detectedExtension is null)
                return Fail(PhotoUploadErrorCode.InvalidSignature);

            if (!ExtensionsCompatible(extension, detectedExtension))
                return Fail(PhotoUploadErrorCode.InvalidSignature);

            if (!PhotoImageStructureValidator.TryValidate(bytes, detectedExtension, out var structureError))
                return Fail(structureError == PhotoUploadErrorCode.None
                    ? PhotoUploadErrorCode.InvalidImage
                    : structureError);

            if (!persist)
                return new InspectOutcome(PhotoUploadResult.Ok(string.Empty));

            var targetFolder = Path.GetFullPath(_options.StorageRootPath!);
            Directory.CreateDirectory(targetFolder);

            var newFileName = $"{Guid.NewGuid():N}{detectedExtension}";
            fullPath = Path.GetFullPath(Path.Combine(targetFolder, newFileName));

            var normalizedFolder = Path.GetFullPath(targetFolder + Path.DirectorySeparatorChar);
            if (!fullPath.StartsWith(normalizedFolder, StringComparison.OrdinalIgnoreCase))
                return Fail(PhotoUploadErrorCode.StorageFailure);

            await using (var fileStream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await fileStream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            }

            return new InspectOutcome(PhotoUploadResult.Ok($"/{LogicalFolder}/{newFileName}"));
        }
        catch
        {
            if (fullPath is not null)
                TryDeleteFile(fullPath);

            return Fail(PhotoUploadErrorCode.StorageFailure);
        }
    }

    private static InspectOutcome Fail(PhotoUploadErrorCode code)
        => new(PhotoUploadResult.Fail(code, PhotoUploadMessages.For(code)));

    private static bool ExtensionsCompatible(string declaredExtension, string detectedExtension)
    {
        var declaredIsJpeg = declaredExtension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                             || declaredExtension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase);
        var detectedIsJpeg = detectedExtension.Equals(".jpg", StringComparison.OrdinalIgnoreCase);

        if (declaredIsJpeg)
            return detectedIsJpeg;

        return declaredExtension.Equals(detectedExtension, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasDisallowedCompoundExtension(string fileName)
    {
        // photo.jpg.exe → uzantı .exe (allowlist dışı). document.pdf.jpg / a.php.jpg → iç içe uzantı.
        var withoutFinal = Path.GetFileNameWithoutExtension(fileName);
        if (string.IsNullOrEmpty(withoutFinal))
            return true;

        var innerExtension = Path.GetExtension(withoutFinal);
        return !string.IsNullOrEmpty(innerExtension);
    }

    private bool TryResolveSafePhysicalPath(string? logicalPhotoPath, out string fullPath)
    {
        fullPath = string.Empty;

        if (string.IsNullOrWhiteSpace(logicalPhotoPath) || string.IsNullOrWhiteSpace(_options.StorageRootPath))
            return false;

        var normalizedRelative = logicalPhotoPath.Replace('\\', '/').Trim();
        if (normalizedRelative.Contains("..", StringComparison.Ordinal)
            || normalizedRelative.Contains(':', StringComparison.Ordinal))
        {
            return false;
        }

        normalizedRelative = normalizedRelative.TrimStart('/');

        if (Path.IsPathRooted(normalizedRelative.Replace('/', Path.DirectorySeparatorChar)))
            return false;

        const string prefix = LogicalFolder + "/";
        if (!normalizedRelative.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var fileName = Path.GetFileName(normalizedRelative);
        if (string.IsNullOrWhiteSpace(fileName) || !GeneratedPhotoFileName.IsMatch(fileName))
            return false;

        if (!string.Equals(normalizedRelative, prefix + fileName, StringComparison.OrdinalIgnoreCase))
            return false;

        var storageRoot = Path.GetFullPath(_options.StorageRootPath + Path.DirectorySeparatorChar);
        fullPath = Path.GetFullPath(Path.Combine(_options.StorageRootPath, fileName));

        return fullPath.StartsWith(storageRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveContentType(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        if (ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
            return "image/jpeg";

        if (ext.Equals(".png", StringComparison.OrdinalIgnoreCase))
            return "image/png";

        return null;
    }

    private static async Task<long> CopyLimitedAsync(
        Stream source,
        Stream destination,
        long maxBytesInclusive,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[CopyBufferSize];
        long total = 0;

        while (total < maxBytesInclusive)
        {
            var toRead = (int)Math.Min(buffer.Length, maxBytesInclusive - total);
            var read = await source.ReadAsync(buffer.AsMemory(0, toRead), cancellationToken).ConfigureAwait(false);
            if (read == 0)
                break;

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            total += read;
        }

        return total;
    }

    private static string? DetectImageType(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return ".jpg";

        if (header.Length >= 8 &&
            header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
            header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
            return ".png";

        return null;
    }

    private static void TryDeleteFile(string fullPath)
    {
        try
        {
            if (File.Exists(fullPath))
                File.Delete(fullPath);
        }
        catch
        {
            // Kısmi dosya temizliği best-effort.
        }
    }

    private readonly record struct InspectOutcome(PhotoUploadResult Result);
}
