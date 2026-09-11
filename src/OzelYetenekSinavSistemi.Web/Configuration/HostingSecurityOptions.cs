using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using AspNetIpNetwork = Microsoft.AspNetCore.HttpOverrides.IPNetwork;

namespace OzelYetenekSinavSistemi.Web.Configuration;

public sealed class ReverseProxyOptions
{
    public const string SectionName = "ReverseProxy";
    public const int MinForwardLimit = 1;
    public const int MaxForwardLimit = 5;

    public bool Enabled { get; set; }
    public int ForwardLimit { get; set; } = 1;
    public bool RequireHeaderSymmetry { get; set; } = true;
    public string[] KnownProxies { get; set; } = Array.Empty<string>();
    public string[] KnownNetworks { get; set; } = Array.Empty<string>();
}

public sealed class PublicUrlOptions
{
    public const string SectionName = "PublicUrl";

    /// <summary>Mutlak HTTPS taban adres (örn. https://localhost:7102).</summary>
    public string BaseUrl { get; set; } = string.Empty;
}

/// <summary>
/// ReverseProxy / PublicUrl / AllowedHosts startup doğrulaması.
/// Secret veya connection string içermez.
/// </summary>
public sealed class HostingSecurityOptionsValidator
{
    public static void Validate(
        IHostEnvironment environment,
        IConfiguration configuration,
        ReverseProxyOptions reverseProxy,
        PublicUrlOptions publicUrl)
    {
        var errors = new List<string>();
        var isProduction = environment.IsProduction();

        ValidatePublicUrl(publicUrl, isProduction, errors);
        ValidateReverseProxy(reverseProxy, isProduction, errors);
        ValidateAllowedHosts(configuration, publicUrl, isProduction, errors);

        if (errors.Count > 0)
            throw new InvalidOperationException(string.Join(" ", errors));
    }

    public static IReadOnlyList<IPAddress> ParseKnownProxies(ReverseProxyOptions options, List<string>? errors = null)
    {
        var list = new List<IPAddress>();
        foreach (var raw in options.KnownProxies ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            if (!IPAddress.TryParse(raw.Trim(), out var ip))
            {
                errors?.Add($"ReverseProxy:KnownProxies geçersiz IP içeriyor: '{raw.Trim()}'.");
                continue;
            }

            list.Add(ip);
        }

        return list;
    }

    public static IReadOnlyList<AspNetIpNetwork> ParseKnownNetworks(ReverseProxyOptions options, List<string>? errors = null)
    {
        var list = new List<AspNetIpNetwork>();
        foreach (var raw in options.KnownNetworks ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            var value = raw.Trim();
            if (!TryParseCidr(value, out var network, out var parseError))
            {
                errors?.Add(parseError ?? $"ReverseProxy:KnownNetworks geçersiz CIDR: '{value}'.");
                continue;
            }

            if (IsUnrestrictedNetwork(network))
            {
                errors?.Add($"ReverseProxy:KnownNetworks tüm interneti kapsayan ağ kabul edilmez: '{value}'.");
                continue;
            }

            list.Add(network);
        }

        return list;
    }

    public static bool TryNormalizePublicBaseUrl(string? baseUrl, out string normalized, out string? error)
    {
        normalized = string.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            error = "PublicUrl:BaseUrl zorunludur.";
            return false;
        }

        if (!Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri))
        {
            error = "PublicUrl:BaseUrl mutlak bir URI olmalıdır.";
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            error = "PublicUrl:BaseUrl yalnızca https olmalıdır.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(uri.Host))
        {
            error = "PublicUrl:BaseUrl host boş olamaz.";
            return false;
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            error = "PublicUrl:BaseUrl UserInfo içeremez.";
            return false;
        }

        if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
        {
            error = "PublicUrl:BaseUrl query veya fragment içeremez.";
            return false;
        }

        // Path korunur; trailing slash kaldırılır (kök hariç).
        var builder = new UriBuilder(uri)
        {
            Scheme = Uri.UriSchemeHttps,
            Port = uri.IsDefaultPort ? -1 : uri.Port,
            Query = string.Empty,
            Fragment = string.Empty
        };

        var path = builder.Path;
        if (path.Length > 1 && path.EndsWith('/'))
            builder.Path = path.TrimEnd('/');

        normalized = builder.Uri.AbsoluteUri.TrimEnd('/');
        return true;
    }

    private static void ValidatePublicUrl(PublicUrlOptions publicUrl, bool isProduction, List<string> errors)
    {
        if (!TryNormalizePublicBaseUrl(publicUrl.BaseUrl, out var normalized, out var error))
        {
            if (isProduction || !string.IsNullOrWhiteSpace(publicUrl.BaseUrl))
                errors.Add(error ?? "PublicUrl:BaseUrl geçersiz.");
            return;
        }

        publicUrl.BaseUrl = normalized;

        if (isProduction
            && Uri.TryCreate(normalized, UriKind.Absolute, out var uri)
            && IsLoopbackHost(uri.Host))
        {
            errors.Add("Production ortamında PublicUrl:BaseUrl localhost/loopback olamaz.");
        }
    }

    private static void ValidateReverseProxy(ReverseProxyOptions options, bool isProduction, List<string> errors)
    {
        if (options.ForwardLimit < ReverseProxyOptions.MinForwardLimit
            || options.ForwardLimit > ReverseProxyOptions.MaxForwardLimit)
        {
            errors.Add(
                $"ReverseProxy:ForwardLimit {ReverseProxyOptions.MinForwardLimit}-{ReverseProxyOptions.MaxForwardLimit} aralığında olmalıdır.");
        }

        var proxies = ParseKnownProxies(options, errors);
        var networks = ParseKnownNetworks(options, errors);

        if (options.Enabled && isProduction && proxies.Count == 0 && networks.Count == 0)
        {
            errors.Add(
                "Production ortamında ReverseProxy:Enabled=true iken en az bir KnownProxies veya KnownNetworks değeri zorunludur.");
        }
    }

    private static void ValidateAllowedHosts(
        IConfiguration configuration,
        PublicUrlOptions publicUrl,
        bool isProduction,
        List<string> errors)
    {
        var allowedHostsRaw = configuration["AllowedHosts"] ?? string.Empty;
        var hosts = SplitHosts(allowedHostsRaw);

        if (!isProduction)
            return;

        if (hosts.Count == 0)
        {
            errors.Add("Production ortamında AllowedHosts boş olamaz.");
            return;
        }

        if (hosts.Any(h => h == "*" || h == "[::]" || h.Equals("0.0.0.0", StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add("Production ortamında AllowedHosts wildcard veya 0.0.0.0 kabul edilmez.");
            return;
        }

        if (hosts.All(IsLoopbackHost))
        {
            errors.Add("Production ortamında AllowedHosts yalnız localhost/loopback olamaz.");
            return;
        }

        if (!TryNormalizePublicBaseUrl(publicUrl.BaseUrl, out _, out _))
            return;

        if (!Uri.TryCreate(publicUrl.BaseUrl, UriKind.Absolute, out var publicUri))
            return;

        var publicHost = publicUri.Host;
        if (!HostMatchesAllowed(publicHost, hosts))
        {
            errors.Add("Production ortamında PublicUrl.BaseUrl host değeri AllowedHosts içinde bulunmalıdır.");
        }
    }

    internal static IReadOnlyList<string> SplitHosts(string allowedHosts) =>
        allowedHosts
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .ToArray();

    public static bool IsLoopbackHost(string host) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
        || host.Equals("::1", StringComparison.OrdinalIgnoreCase)
        || host.Equals("[::1]", StringComparison.OrdinalIgnoreCase);

    public static bool HostMatchesAllowed(string host, IReadOnlyList<string> allowedHosts)
    {
        foreach (var pattern in allowedHosts)
        {
            if (pattern.StartsWith("*.", StringComparison.Ordinal))
            {
                // Varsayılan olarak desteklenmez; yalnızca açıkça yapılandırılmışsa (kullanıcı isterse).
                // Spec: "varsayılan olmasın" — yine de karşılaştırma için destekleyebiliriz ama
                // Production validation wildcard * reddeder; *.example.com farklıdır.
                var suffix = pattern[1..]; // .example.com
                if (host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                    && host.Length > suffix.Length)
                    return true;
                continue;
            }

            if (string.Equals(host, pattern, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool TryParseCidr(string value, out AspNetIpNetwork network, out string? error)
    {
        network = null!;
        error = null;

        var parts = value.Split('/', StringSplitOptions.TrimEntries);
        if (parts.Length != 2
            || !IPAddress.TryParse(parts[0], out var prefix)
            || !int.TryParse(parts[1], out var prefixLength))
        {
            error = $"ReverseProxy:KnownNetworks geçersiz CIDR: '{value}'.";
            return false;
        }

        var max = prefix.AddressFamily == AddressFamily.InterNetwork ? 32 : 128;
        if (prefixLength < 0 || prefixLength > max)
        {
            error = $"ReverseProxy:KnownNetworks geçersiz prefix uzunluğu: '{value}'.";
            return false;
        }

        network = new AspNetIpNetwork(prefix, prefixLength);
        return true;
    }

    private static bool IsUnrestrictedNetwork(AspNetIpNetwork network) =>
        network.PrefixLength == 0;
}

/// <summary>Options binding sonrası ValidateOnStart için ince sarmalayıcı.</summary>
public sealed class ReverseProxyOptionsValidator : IValidateOptions<ReverseProxyOptions>
{
    private readonly IHostEnvironment _environment;

    public ReverseProxyOptionsValidator(IHostEnvironment environment) => _environment = environment;

    public ValidateOptionsResult Validate(string? name, ReverseProxyOptions options)
    {
        var errors = new List<string>();
        HostingSecurityOptionsValidator.ParseKnownProxies(options, errors);
        HostingSecurityOptionsValidator.ParseKnownNetworks(options, errors);

        if (options.ForwardLimit < ReverseProxyOptions.MinForwardLimit
            || options.ForwardLimit > ReverseProxyOptions.MaxForwardLimit)
        {
            errors.Add(
                $"ReverseProxy:ForwardLimit {ReverseProxyOptions.MinForwardLimit}-{ReverseProxyOptions.MaxForwardLimit} aralığında olmalıdır.");
        }

        if (options.Enabled
            && _environment.IsProduction()
            && (options.KnownProxies?.Length ?? 0) == 0
            && (options.KnownNetworks?.Length ?? 0) == 0)
        {
            errors.Add(
                "Production ortamında ReverseProxy:Enabled=true iken KnownProxies veya KnownNetworks zorunludur.");
        }

        return errors.Count > 0 ? ValidateOptionsResult.Fail(errors) : ValidateOptionsResult.Success;
    }
}

public sealed class PublicUrlOptionsValidator : IValidateOptions<PublicUrlOptions>
{
    private readonly IHostEnvironment _environment;

    public PublicUrlOptionsValidator(IHostEnvironment environment) => _environment = environment;

    public ValidateOptionsResult Validate(string? name, PublicUrlOptions options)
    {
        if (!HostingSecurityOptionsValidator.TryNormalizePublicBaseUrl(options.BaseUrl, out var normalized, out var error))
        {
            if (_environment.IsProduction() || !string.IsNullOrWhiteSpace(options.BaseUrl))
                return ValidateOptionsResult.Fail(error ?? "PublicUrl:BaseUrl geçersiz.");
            return ValidateOptionsResult.Success;
        }

        options.BaseUrl = normalized;

        if (_environment.IsProduction()
            && Uri.TryCreate(normalized, UriKind.Absolute, out var uri)
            && HostingSecurityOptionsValidator.IsLoopbackHost(uri.Host))
        {
            return ValidateOptionsResult.Fail(
                "Production ortamında PublicUrl:BaseUrl localhost/loopback olamaz.");
        }

        return ValidateOptionsResult.Success;
    }
}
