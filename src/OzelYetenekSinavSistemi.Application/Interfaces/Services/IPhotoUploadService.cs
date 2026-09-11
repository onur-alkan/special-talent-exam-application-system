using OzelYetenekSinavSistemi.Application.Common;

namespace OzelYetenekSinavSistemi.Application.Interfaces.Services;

/// <summary>
/// Güvenli vesikalık fotoğraf yükleme/okuma/silme servisi.
/// Fiziksel dosyalar private storage altında tutulur; mantıksal yol /uploads/photos/{guid}.ext biçimindedir.
/// </summary>
public interface IPhotoUploadService
{
    /// <summary>
    /// Depolamaya yazmadan uzantı/MIME/imza/yapı/boyut doğrulaması yapar.
    /// Geçersiz dosyada hiçbir kalıcı dosya oluşturulmaz.
    /// </summary>
    Task<PhotoUploadResult> ValidatePhotoAsync(PhotoUploadRequest request, CancellationToken cancellationToken = default);

    /// <summary>Başarılıysa mantıksal yolu döner (örn: /uploads/photos/xxx.jpg).</summary>
    Task<PhotoUploadResult> SavePhotoAsync(PhotoUploadRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Yalnızca uygulamanın oluşturduğu /uploads/photos/ altındaki GUID adlı fotoğrafları siler.
    /// Dosya yoksa başarılı (idempotent) kabul edilir. Geçersiz yollar reddedilir.
    /// </summary>
    Task<bool> DeletePhotoAsync(string? relativePhotoPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mantıksal PhotoPath üzerinden private storage'dan güvenli okuma yapar.
    /// </summary>
    Task<OperationResult<PhotoFileContent>> ReadPhotoAsync(
        string? logicalPhotoPath,
        CancellationToken cancellationToken = default);
}
