namespace OzelYetenekSinavSistemi.Application.Common;

/// <summary>
/// Yetkilendirilmiş fotoğraf okuma sonucu (fiziksel yol içermez).
/// </summary>
public sealed class PhotoFileContent
{
    public required byte[] Content { get; init; }
    public required string ContentType { get; init; }
    public required string FileName { get; init; }
}
