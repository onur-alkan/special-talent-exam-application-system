using OzelYetenekSinavSistemi.Application.Common;

namespace OzelYetenekSinavSistemi.Web.Infrastructure;

/// <summary>
/// IFormFile → PhotoUploadRequest eşlemesi.
/// İçeriği MemoryStream'e kopyalamaz; OpenReadStream kullanır.
/// Gerçek bayt sınırı PhotoUploadService.CopyLimitedAsync ile uygulanır.
/// </summary>
public static class PhotoUploadFormMapper
{
    public static PhotoUploadRequest ToRequest(IFormFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        return new PhotoUploadRequest
        {
            Content = file.OpenReadStream(),
            FileName = file.FileName ?? string.Empty,
            ContentType = file.ContentType ?? string.Empty,
            Length = file.Length
        };
    }
}
