using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using QRCoder;

namespace OzelYetenekSinavSistemi.Infrastructure.Services;

/// <summary>
/// QRCoder tabanlı QR üretimi. PngByteQRCode kullanılır; System.Drawing bağımlılığı yoktur.
/// </summary>
public sealed class QrCodeService : IQrCodeService
{
    public byte[] GeneratePng(string content)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data);
        return png.GetGraphic(20);
    }

    public string GenerateBase64PngDataUri(string content)
    {
        var bytes = GeneratePng(content);
        return "data:image/png;base64," + Convert.ToBase64String(bytes);
    }
}
