namespace OzelYetenekSinavSistemi.Application.Common;

/// <summary>
/// Web katmanındaki IFormFile bağımlılığını Application katmanından uzak tutmak için
/// kullanılan dosya yükleme isteği modeli.
/// </summary>
public sealed class PhotoUploadRequest
{
    public required Stream Content { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public long Length { get; init; }
}
