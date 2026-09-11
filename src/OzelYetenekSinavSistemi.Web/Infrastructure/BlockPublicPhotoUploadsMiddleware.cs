namespace OzelYetenekSinavSistemi.Web.Infrastructure;

/// <summary>
/// /uploads/photos altındaki istekleri static files'a ulaşmadan 404 ile sonlandırır.
/// </summary>
public sealed class BlockPublicPhotoUploadsMiddleware
{
    private readonly RequestDelegate _next;

    public BlockPublicPhotoUploadsMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (IsBlockedPhotoPath(path))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await _next(context).ConfigureAwait(false);
    }

    public static bool IsBlockedPhotoPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var normalized = path.Replace('\\', '/');
        return normalized.Equals("/uploads/photos", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("/uploads/photos/", StringComparison.OrdinalIgnoreCase);
    }
}
