namespace OzelYetenekSinavSistemi.Application.Common;

/// <summary>
/// Vesikalık fotoğraf yükleme kullanıcı mesajları.
/// </summary>
public static class PhotoUploadMessages
{
    public const string Required = "Vesikalık fotoğrafınızı seçiniz.";
    public const string Empty = "Seçilen fotoğraf dosyası boş olamaz.";
    public const string UnsupportedType = "Yalnızca geçerli JPG, JPEG veya PNG fotoğraf yükleyiniz.";
    public const string InvalidContent = "Seçilen fotoğraf dosyası okunamadı. Lütfen geçerli bir fotoğraf seçiniz.";
    public const string TooLarge = "Fotoğraf dosyası en fazla 2 MB olabilir.";
    public const string GenericFailure = "Fotoğraf yüklenemedi. Lütfen başka bir fotoğrafla yeniden deneyiniz.";
    public const string ReselectHint = "Hata durumunda fotoğrafı ve parolaları yeniden girmeniz gerekir.";
    public const string ProfileKeepHint = "Yeni bir fotoğraf seçmezseniz mevcut fotoğrafınız korunacaktır.";
    public const string ProfileReselectHint = "Hata durumunda fotoğrafı yeniden seçmeniz gerekir.";
    public const string ProfileUpdated = "Profil fotoğrafınız başarıyla güncellendi.";
    public const string ProfileUpdatedGeneral = "Profil bilgileriniz güncellendi.";

    public static string For(PhotoUploadErrorCode code) => code switch
    {
        PhotoUploadErrorCode.Missing => Required,
        PhotoUploadErrorCode.Empty => Empty,
        PhotoUploadErrorCode.UnsupportedExtension => UnsupportedType,
        PhotoUploadErrorCode.UnsupportedMimeType => UnsupportedType,
        PhotoUploadErrorCode.InvalidSignature => UnsupportedType,
        PhotoUploadErrorCode.InvalidImage => InvalidContent,
        PhotoUploadErrorCode.TooLarge => TooLarge,
        PhotoUploadErrorCode.InvalidFileName => UnsupportedType,
        PhotoUploadErrorCode.StorageFailure => GenericFailure,
        _ => GenericFailure
    };

    public static bool IsPhotoError(string? message)
        => !string.IsNullOrWhiteSpace(message)
           && (message == Required
               || message == Empty
               || message == UnsupportedType
               || message == InvalidContent
               || message == TooLarge
               || message == GenericFailure);
}
