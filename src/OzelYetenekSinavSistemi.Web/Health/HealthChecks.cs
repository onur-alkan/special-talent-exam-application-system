using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using OzelYetenekSinavSistemi.Infrastructure.Configuration;
using OzelYetenekSinavSistemi.Web.Configuration;

namespace OzelYetenekSinavSistemi.Web.Health;

internal static class HealthCheckTags
{
    public const string Ready = "ready";
}

public sealed class SqlSelectOneHealthCheck : IHealthCheck
{
    private readonly string _connectionString;
    private readonly TimeSpan _timeout;

    public SqlSelectOneHealthCheck(IConfiguration configuration, IHostEnvironment environment)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
        _timeout = TimeSpan.FromSeconds(5);
        _ = environment;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_timeout);
            await ProductionReadinessValidator.CanOpenSqlAsync(_connectionString, _timeout, cts.Token)
                .ConfigureAwait(false);
            return HealthCheckResult.Healthy();
        }
        catch (Exception)
        {
            return HealthCheckResult.Unhealthy("sql_unavailable");
        }
    }
}

public sealed class PhotoStorageHealthCheck : IHealthCheck
{
    private readonly IOptions<PhotoUploadOptions> _options;
    private readonly IHostEnvironment _environment;

    public PhotoStorageHealthCheck(IOptions<PhotoUploadOptions> options, IHostEnvironment environment)
    {
        _options = options;
        _environment = environment;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        ProductionReadinessValidator.EnsurePrivatePhotoStorageAccessible(
            _options.Value, _environment, errors);
        return Task.FromResult(
            errors.Count == 0
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("photo_storage_unavailable"));
    }
}

public sealed class DataProtectionKeysHealthCheck : IHealthCheck
{
    private readonly IHostEnvironment _environment;
    private readonly DataProtectionOptions _options;

    public DataProtectionKeysHealthCheck(
        IHostEnvironment environment,
        IConfiguration configuration)
    {
        _environment = environment;
        _options = configuration.GetSection(DataProtectionOptions.SectionName).Get<DataProtectionOptions>()
                   ?? new DataProtectionOptions();
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var keysDirectory = DataProtectionKeyRing.ResolveKeysDirectory(_environment, _options);
            var errors = new List<string>();
            ProductionReadinessValidator.EnsureDataProtectionKeysAccessible(keysDirectory, errors);
            return Task.FromResult(
                errors.Count == 0
                    ? HealthCheckResult.Healthy()
                    : HealthCheckResult.Unhealthy("dataprotection_keys_unavailable"));
        }
        catch (Exception)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("dataprotection_keys_unavailable"));
        }
    }
}

public sealed class CriticalConfigurationHealthCheck : IHealthCheck
{
    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public CriticalConfigurationHealthCheck(IHostEnvironment environment, IConfiguration configuration)
    {
        _environment = environment;
        _configuration = configuration;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_environment.IsProduction())
            return Task.FromResult(HealthCheckResult.Healthy());

        var errors = new List<string>();
        ProductionReadinessValidator.ValidateConnectionStringCore(
            _configuration.GetConnectionString("DefaultConnection"), errors);

        var seed = _configuration.GetSection(SeedOptions.SectionName).Get<SeedOptions>() ?? new SeedOptions();
        if (seed.SeedTestData)
            errors.Add("seed_test_data_enabled");

        var publicUrl = _configuration.GetSection(PublicUrlOptions.SectionName).Get<PublicUrlOptions>()
                        ?? new PublicUrlOptions();
        var reverseProxy = _configuration.GetSection(ReverseProxyOptions.SectionName).Get<ReverseProxyOptions>()
                           ?? new ReverseProxyOptions();
        try
        {
            HostingSecurityOptionsValidator.Validate(_environment, _configuration, reverseProxy, publicUrl);
        }
        catch (InvalidOperationException)
        {
            errors.Add("hosting_security_invalid");
        }

        return Task.FromResult(
            errors.Count == 0
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("configuration_invalid"));
    }
}

public static class HealthCheckResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static Task WriteLiveAsync(HttpContext context, HealthReport report)
    {
        ApplyNoStoreHeaders(context);
        context.Response.ContentType = "application/json; charset=utf-8";
        var payload = new { status = ToStatus(report.Status) };
        return context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions));
    }

    public static Task WriteReadyAsync(HttpContext context, HealthReport report)
    {
        ApplyNoStoreHeaders(context);
        context.Response.ContentType = "application/json; charset=utf-8";

        var checks = report.Entries
            .OrderBy(e => e.Key, StringComparer.Ordinal)
            .Select(e => new
            {
                name = SanitizeCheckName(e.Key),
                status = ToStatus(e.Value.Status)
            })
            .ToArray();

        var payload = new
        {
            status = ToStatus(report.Status),
            checks
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions));
    }

    public static void ApplyNoStoreHeaders(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store, no-cache";
        context.Response.Headers.Pragma = "no-cache";
    }

    private static string ToStatus(HealthStatus status) => status switch
    {
        HealthStatus.Healthy => "Healthy",
        HealthStatus.Degraded => "Degraded",
        _ => "Unhealthy"
    };

    private static string SanitizeCheckName(string name)
    {
        // Yalnız güvenli tanımlayıcılar; yol/exception taşımaz.
        var cleaned = new string(name.Where(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-').ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "check" : cleaned;
    }
}
