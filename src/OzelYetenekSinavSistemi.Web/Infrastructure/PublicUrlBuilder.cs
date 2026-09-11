using Microsoft.Extensions.Options;
using OzelYetenekSinavSistemi.Web.Configuration;

namespace OzelYetenekSinavSistemi.Web.Infrastructure;

public interface IPublicUrlBuilder
{
    string BuildPasswordResetBaseUrl();
    string BuildDocumentVerificationBaseUrl();
    string BuildPath(string relativePath);
}

public sealed class PublicUrlBuilder : IPublicUrlBuilder
{
    private readonly Uri _baseUri;

    public PublicUrlBuilder(IOptions<PublicUrlOptions> options)
    {
        var configured = options.Value.BaseUrl;
        if (!HostingSecurityOptionsValidator.TryNormalizePublicBaseUrl(configured, out var normalized, out var error))
            throw new InvalidOperationException(error ?? "PublicUrl:BaseUrl geçersiz.");

        _baseUri = new Uri(normalized.TrimEnd('/') + "/", UriKind.Absolute);
    }

    public string BuildPasswordResetBaseUrl() => BuildPath("/Account/ResetPassword");

    public string BuildDocumentVerificationBaseUrl() => BuildPath("/DocumentVerification/Verify");

    public string BuildPath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("relativePath zorunludur.", nameof(relativePath));

        // Host/scheme kullanıcı girdisinden üretilmez; yalnızca yapılandırılmış BaseUrl.
        var relative = relativePath.Trim().TrimStart('/');
        return new Uri(_baseUri, relative).AbsoluteUri;
    }
}
