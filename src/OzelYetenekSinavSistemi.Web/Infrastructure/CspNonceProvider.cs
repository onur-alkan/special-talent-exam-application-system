using System.Security.Cryptography;

namespace OzelYetenekSinavSistemi.Web.Infrastructure;

/// <summary>
/// İstek bazlı kriptografik CSP nonce üretici.
/// Nonce parola/token değildir; loglanmamalıdır.
/// </summary>
public interface ICspNonceProvider
{
    /// <summary>Aynı istek boyunca sabit, farklı isteklerde farklı Base64 nonce.</summary>
    string GetNonce(HttpContext httpContext);
}

public sealed class CspNonceProvider : ICspNonceProvider
{
    public const string HttpContextItemKey = "Oys.CspNonce";
    private const int NonceByteLength = 16; // 128 bit

    public string GetNonce(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (httpContext.Items.TryGetValue(HttpContextItemKey, out var existing)
            && existing is string cached
            && !string.IsNullOrEmpty(cached))
        {
            return cached;
        }

        Span<byte> bytes = stackalloc byte[NonceByteLength];
        RandomNumberGenerator.Fill(bytes);
        var nonce = Convert.ToBase64String(bytes);

        // Header injection karakterleri Base64'te oluşmaz; yine de doğrula.
        if (nonce.IndexOfAny(['\r', '\n', '\0']) >= 0)
            throw new InvalidOperationException("Üretilen CSP nonce geçersiz karakter içeriyor.");

        httpContext.Items[HttpContextItemKey] = nonce;
        return nonce;
    }
}
