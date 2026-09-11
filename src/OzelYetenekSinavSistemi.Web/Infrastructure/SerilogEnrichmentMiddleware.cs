using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using Serilog.Context;

namespace OzelYetenekSinavSistemi.Web.Infrastructure;

/// <summary>
/// Her istek için Serilog LogContext'e güvenli alanlar ekler.
/// QueryString, request body, header ve cookie loglanmaz.
/// </summary>
public sealed class SerilogEnrichmentMiddleware
{
    private readonly RequestDelegate _next;

    public SerilogEnrichmentMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(
        HttpContext context,
        ISensitiveDataMaskingService masking,
        IClientIpAddressAccessor clientIp)
    {
        var userId = context.User.GetUserId();
        var ip = masking.SanitizeIpAddress(clientIp.GetClientIpString(context));
        // Yalnızca Path; QueryString asla enrichment'a eklenmez (reset token sızıntısını önler).
        var path = masking.SanitizeRequestPath(context.Request.Path.Value);
        var method = masking.SanitizeForLog(context.Request.Method, 16);
        var correlationId = masking.SanitizeCorrelationId(context.TraceIdentifier);

        using (LogContext.PushProperty("UserId", userId == Guid.Empty ? null : userId.ToString()))
        using (LogContext.PushProperty("IpAddress", ip))
        using (LogContext.PushProperty("RequestPath", path))
        using (LogContext.PushProperty("RequestMethod", method))
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
