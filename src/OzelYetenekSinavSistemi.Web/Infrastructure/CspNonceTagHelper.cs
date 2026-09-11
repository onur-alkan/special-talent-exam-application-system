using Microsoft.AspNetCore.Razor.TagHelpers;

namespace OzelYetenekSinavSistemi.Web.Infrastructure;

/// <summary>
/// Inline script/style elementlerine istek CSP nonce'unu ekler.
/// Mevcut nonce attribute'u varsa üzerine yazmaz.
/// </summary>
[HtmlTargetElement("script")]
[HtmlTargetElement("style")]
public sealed class CspNonceTagHelper : TagHelper
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ICspNonceProvider _nonceProvider;

    public CspNonceTagHelper(IHttpContextAccessor httpContextAccessor, ICspNonceProvider nonceProvider)
    {
        _httpContextAccessor = httpContextAccessor;
        _nonceProvider = nonceProvider;
    }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (output.Attributes.ContainsName("nonce"))
            return;

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
            return;

        var nonce = _nonceProvider.GetNonce(httpContext);
        output.Attributes.SetAttribute("nonce", nonce);
    }
}
