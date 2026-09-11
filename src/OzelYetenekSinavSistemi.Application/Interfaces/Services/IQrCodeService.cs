namespace OzelYetenekSinavSistemi.Application.Interfaces.Services;

public interface IQrCodeService
{
    /// <summary>Verilen metinden PNG QR kod üretir ve base64 data-uri döner.</summary>
    string GenerateBase64PngDataUri(string content);

    byte[] GeneratePng(string content);
}
