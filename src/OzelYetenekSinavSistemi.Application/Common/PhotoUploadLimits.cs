namespace OzelYetenekSinavSistemi.Application.Common;

/// <summary>
/// Vesikalık fotoğraf yükleme boyut ve biçim sınırları (tek merkezî politika).
/// </summary>
public static class PhotoUploadLimits
{
    /// <summary>Tek fotoğraf dosyası üst sınırı (2 MB = 2.097.152 bayt). Yapılandırma ile aşılamaz.</summary>
    public const long MaxFileBytes = 2 * 1024 * 1024;

    /// <summary>Kullanıcıya gösterilen boyut metni (MaxFileBytes ile eşleşmeli).</summary>
    public const string MaxFileSizeDisplay = "2 MB";

    /// <summary>
    /// Multipart HTTP isteğinin tamamı için üst sınır (3 MB).
    /// Dosya + form alanları + MIME sınırları için dosya sınırından büyük tutulur.
    /// </summary>
    public const long MaxMultipartRequestBytes = 3 * 1024 * 1024;

    /// <summary>FormOptions.MemoryBufferThreshold — küçük parçalar disk yerine bellekte.</summary>
    public const int MemoryBufferThresholdBytes = 64 * 1024;

    public const long MinConfigurableFileBytes = 1024;

    /// <summary>Decompression bomb koruması: genişlik × yükseklik üst sınırı.</summary>
    public const long MaxTotalPixels = 25_000_000;

    public const string AcceptAttribute = ".jpg,.jpeg,.png,image/jpeg,image/png";

    public static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png"];

    public static readonly string[] AllowedContentTypes = ["image/jpeg", "image/png"];

    public static bool IsAllowedExtension(string? extension)
        => !string.IsNullOrWhiteSpace(extension)
           && AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);

    public static bool IsAllowedContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return false;

        var normalized = contentType.Trim();
        var semicolon = normalized.IndexOf(';', StringComparison.Ordinal);
        if (semicolon >= 0)
            normalized = normalized[..semicolon].Trim();

        return AllowedContentTypes.Contains(normalized, StringComparer.OrdinalIgnoreCase);
    }
}
