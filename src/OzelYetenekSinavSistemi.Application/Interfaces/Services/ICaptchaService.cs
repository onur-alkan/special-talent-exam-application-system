namespace OzelYetenekSinavSistemi.Application.Interfaces.Services;

public sealed record CaptchaChallenge(string Code, byte[] ImageBytes, string ContentType);

/// <summary>
/// Sunucu tarafında doğrulanan, dış API gerektirmeyen lokal captcha.
/// </summary>
public interface ICaptchaService
{
    CaptchaChallenge Generate();

    /// <summary>Beklenen kod ile kullanıcı girdisini büyük/küçük harf duyarsız karşılaştırır.</summary>
    bool Validate(string? expectedCode, string? userInput);
}
