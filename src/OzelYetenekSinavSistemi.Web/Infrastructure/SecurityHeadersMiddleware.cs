namespace OzelYetenekSinavSistemi.Web.Infrastructure;

/// <summary>
/// Temel güvenlik başlıklarını (CSP, X-Content-Type-Options, X-Frame-Options,
/// Referrer-Policy, Permissions-Policy, COOP/CORP) ekleyen middleware.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ICspNonceProvider _cspNonceProvider;

    public SecurityHeadersMiddleware(RequestDelegate next, ICspNonceProvider cspNonceProvider)
    {
        _next = next;
        _cspNonceProvider = cspNonceProvider;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var nonce = _cspNonceProvider.GetNonce(context);
        ApplyHeaders(context.Response.Headers, nonce);
        await _next(context);
    }

    /// <summary>Test ve dokümantasyon için sabit CSP şablonu.</summary>
    public static string BuildContentSecurityPolicy(string nonce)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nonce);
        if (nonce.IndexOfAny(['\r', '\n', '\0', ';', ',']) >= 0)
            throw new ArgumentException("CSP nonce geçersiz karakter içeriyor.", nameof(nonce));

        return
            "default-src 'self'; " +
            $"script-src 'self' 'nonce-{nonce}'; " +
            "script-src-attr 'none'; " +
            "style-src 'self' 'unsafe-inline'; " +
            "img-src 'self' data:; " +
            "font-src 'self' data:; " +
            "connect-src 'self'; " +
            "object-src 'none'; " +
            "frame-src 'none'; " +
            "frame-ancestors 'self'; " +
            "base-uri 'self'; " +
            "form-action 'self'; " +
            "manifest-src 'self'; " +
            "worker-src 'self'";
    }

    internal static void ApplyHeaders(IHeaderDictionary headers, string nonce)
    {
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "SAMEORIGIN";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        headers["X-Permitted-Cross-Domain-Policies"] = "none";
        headers["Cross-Origin-Opener-Policy"] = "same-origin";
        headers["Cross-Origin-Resource-Policy"] = "same-origin";
        headers["X-XSS-Protection"] = "0";
        headers["Content-Security-Policy"] = BuildContentSecurityPolicy(nonce);
    }
}
