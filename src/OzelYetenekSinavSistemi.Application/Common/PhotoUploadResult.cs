namespace OzelYetenekSinavSistemi.Application.Common;

/// <summary>
/// Typed vesikalık fotoğraf yükleme sonucu.
/// </summary>
public sealed class PhotoUploadResult
{
    public bool Success { get; private init; }
    public string? StoredPath { get; private init; }
    public PhotoUploadErrorCode ErrorCode { get; private init; }
    public string? UserMessage { get; private init; }

    public static PhotoUploadResult Ok(string storedPath) => new()
    {
        Success = true,
        StoredPath = storedPath,
        ErrorCode = PhotoUploadErrorCode.None
    };

    public static PhotoUploadResult Fail(PhotoUploadErrorCode errorCode, string userMessage) => new()
    {
        Success = false,
        ErrorCode = errorCode,
        UserMessage = userMessage
    };
}
