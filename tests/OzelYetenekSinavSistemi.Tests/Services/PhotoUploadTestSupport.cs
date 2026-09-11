using Moq;
using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.Interfaces.Infrastructure;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Infrastructure.Services;

namespace OzelYetenekSinavSistemi.Tests.Services;

internal static class PhotoUploadTestSupport
{
    public static IKeyedAsyncLock SharedLock { get; } = new KeyedAsyncLock();

    /// <summary>1x1 geçerli PNG (yapısal olarak tam).</summary>
    public static byte[] MinimalPng { get; } = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    /// <summary>1x1 geçerli JPEG (yapısal olarak tam).</summary>
    public static byte[] MinimalJpeg { get; } = Convert.FromBase64String(
        "/9j/4AAQSkZJRgABAQEASABIAAD/2wBDAP//////////////////////////////////////////////////////////////////////////////////////2wBDAf//////////////////////////////////////////////////////////////////////////////////////wAARCAABAAEDAREAAhEBAxEB/8QAFAABAAAAAAAAAAAAAAAAAAAACf/EABQQAQAAAAAAAAAAAAAAAAAAAAD/xAAUAQEAAAAAAAAAAAAAAAAAAAAA/8QAFBEBAAAAAAAAAAAAAAAAAAAAAP/aAAwDAQACEQMRAD8AKwA//9k=");

    public static byte[] CreateJpegOfLength(int totalLength)
    {
        if (totalLength < MinimalJpeg.Length)
            throw new ArgumentOutOfRangeException(nameof(totalLength));

        var content = new byte[totalLength];
        Buffer.BlockCopy(MinimalJpeg, 0, content, 0, MinimalJpeg.Length);
        return content;
    }

    public static byte[] TruncatedJpegHeaderOnly()
        => MinimalJpeg.AsSpan(0, Math.Min(16, MinimalJpeg.Length)).ToArray();

    public static byte[] TruncatedPngHeaderOnly()
        => MinimalPng.AsSpan(0, 8).ToArray();

    /// <summary>Controller testleri için ValidatePhotoAsync'i her zaman başarılı döndüren mock.</summary>
    public static IPhotoUploadService CreateAcceptingPhotoService()
    {
        var mock = new Mock<IPhotoUploadService>();
        mock.Setup(p => p.ValidatePhotoAsync(It.IsAny<PhotoUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PhotoUploadResult.Ok(string.Empty));
        mock.Setup(p => p.SavePhotoAsync(It.IsAny<PhotoUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PhotoUploadResult.Ok("/uploads/photos/" + Guid.NewGuid().ToString("N") + ".jpg"));
        mock.Setup(p => p.DeletePhotoAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return mock.Object;
    }
}
