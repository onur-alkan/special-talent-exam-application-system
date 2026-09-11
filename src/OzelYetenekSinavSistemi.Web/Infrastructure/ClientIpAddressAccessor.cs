using System.Net;
using System.Net.Sockets;

namespace OzelYetenekSinavSistemi.Web.Infrastructure;

/// <summary>
/// ForwardedHeaders sonrası güvenilir istemci IP'si.
/// X-Forwarded-For'u kendisi parse etmez; ham header loglamaz.
/// </summary>
public interface IClientIpAddressAccessor
{
    IPAddress? GetRemoteIpAddress(HttpContext httpContext);

    /// <summary>Audit / log için normalize edilmiş metin; yoksa null.</summary>
    string? GetClientIpString(HttpContext httpContext);

    /// <summary>Rate limit partition anahtarı; null IP için sabit fallback.</summary>
    string GetPartitionKey(HttpContext httpContext);
}

public sealed class ClientIpAddressAccessor : IClientIpAddressAccessor
{
    public const string UnknownPartitionKey = "unknown";

    public IPAddress? GetRemoteIpAddress(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        return Normalize(httpContext.Connection.RemoteIpAddress);
    }

    public string? GetClientIpString(HttpContext httpContext)
    {
        var ip = GetRemoteIpAddress(httpContext);
        return ip?.ToString();
    }

    public string GetPartitionKey(HttpContext httpContext)
    {
        var ip = GetRemoteIpAddress(httpContext);
        return ip?.ToString() ?? UnknownPartitionKey;
    }

    public static IPAddress? Normalize(IPAddress? address)
    {
        if (address is null)
            return null;

        if (address.IsIPv4MappedToIPv6)
            return address.MapToIPv4();

        return address;
    }
}

public static class ClientIpHttpContextExtensions
{
    public static string? GetClientIpString(this HttpContext httpContext)
    {
        var accessor = httpContext.RequestServices?.GetService<IClientIpAddressAccessor>();
        return accessor?.GetClientIpString(httpContext)
               ?? ClientIpAddressAccessor.Normalize(httpContext.Connection.RemoteIpAddress)?.ToString();
    }

    public static string GetClientIpPartitionKey(this HttpContext httpContext)
    {
        var accessor = httpContext.RequestServices?.GetService<IClientIpAddressAccessor>();
        return accessor?.GetPartitionKey(httpContext)
               ?? (ClientIpAddressAccessor.Normalize(httpContext.Connection.RemoteIpAddress)?.ToString()
                   ?? ClientIpAddressAccessor.UnknownPartitionKey);
    }
}
