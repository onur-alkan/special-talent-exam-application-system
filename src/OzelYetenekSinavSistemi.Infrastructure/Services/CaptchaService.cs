using System.Security.Cryptography;
using System.Text;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;

namespace OzelYetenekSinavSistemi.Infrastructure.Services;

/// <summary>
/// Dış API gerektirmeyen, sunucu tarafında doğrulanan lokal captcha.
/// Görsel, platform bağımsız olması için SVG olarak üretilir.
/// </summary>
public sealed class CaptchaService : ICaptchaService
{
    // Karışabilecek karakterler (0/O, 1/I/L) çıkarıldı.
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private const int CodeLength = 5;

    public CaptchaChallenge Generate()
    {
        var code = GenerateCode();
        var svg = BuildSvg(code);
        return new CaptchaChallenge(code, Encoding.UTF8.GetBytes(svg), "image/svg+xml");
    }

    public bool Validate(string? expectedCode, string? userInput)
    {
        if (string.IsNullOrWhiteSpace(expectedCode) || string.IsNullOrWhiteSpace(userInput))
            return false;

        return string.Equals(expectedCode.Trim(), userInput.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static string GenerateCode()
    {
        var sb = new StringBuilder(CodeLength);
        for (var i = 0; i < CodeLength; i++)
            sb.Append(Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)]);
        return sb.ToString();
    }

    private static string BuildSvg(string code)
    {
        var sb = new StringBuilder();
        sb.Append("<svg xmlns='http://www.w3.org/2000/svg' width='160' height='50' viewBox='0 0 160 50'>");
        sb.Append("<rect width='160' height='50' fill='#f3f3f4'/>");

        // Gürültü çizgileri
        for (var i = 0; i < 6; i++)
        {
            var x1 = RandomNumberGenerator.GetInt32(160);
            var y1 = RandomNumberGenerator.GetInt32(50);
            var x2 = RandomNumberGenerator.GetInt32(160);
            var y2 = RandomNumberGenerator.GetInt32(50);
            var c = Colors[RandomNumberGenerator.GetInt32(Colors.Length)];
            sb.Append($"<line x1='{x1}' y1='{y1}' x2='{x2}' y2='{y2}' stroke='{c}' stroke-width='1' opacity='0.4'/>");
        }

        var x = 18;
        foreach (var ch in code)
        {
            var y = 33 + RandomNumberGenerator.GetInt32(-5, 6);
            var rotate = RandomNumberGenerator.GetInt32(-25, 26);
            var color = Colors[RandomNumberGenerator.GetInt32(Colors.Length)];
            sb.Append($"<text x='{x}' y='{y}' font-family='Verdana,monospace' font-size='28' font-weight='bold' fill='{color}' transform='rotate({rotate} {x} {y})'>{ch}</text>");
            x += 28;
        }

        sb.Append("</svg>");
        return sb.ToString();
    }

    private static readonly string[] Colors = { "#1ab394", "#1c84c6", "#23c6c8", "#ed5565", "#676a6c" };
}
