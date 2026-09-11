namespace OzelYetenekSinavSistemi.Application.Common;

/// <summary>
/// Vesikalık fotoğraf yükleme hata kodları.
/// </summary>
public enum PhotoUploadErrorCode
{
    None = 0,
    Missing = 1,
    Empty = 2,
    UnsupportedExtension = 3,
    UnsupportedMimeType = 4,
    InvalidSignature = 5,
    TooLarge = 6,
    InvalidFileName = 7,
    InvalidImage = 8,
    StorageFailure = 9
}
