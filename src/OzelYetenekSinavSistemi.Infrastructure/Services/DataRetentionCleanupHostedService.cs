using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Infrastructure.Configuration;

namespace OzelYetenekSinavSistemi.Infrastructure.Services;

/// <summary>
/// Periyodik veri saklama temizliği. Startup'ı uzun süre engellemez.
/// </summary>
public sealed class DataRetentionCleanupHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DataRetentionOptions _options;
    private readonly ILogger<DataRetentionCleanupHostedService> _logger;

    public DataRetentionCleanupHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<DataRetentionOptions> options,
        ILogger<DataRetentionCleanupHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableCleanup)
        {
            _logger.LogInformation("DataRetentionCleanupHostedService devre dışı.");
            return;
        }

        var startupDelay = TimeSpan.FromSeconds(Math.Clamp(_options.StartupDelaySeconds, 0, 3600));
        if (startupDelay > TimeSpan.Zero)
        {
            try
            {
                await Task.Delay(startupDelay, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        var interval = TimeSpan.FromHours(Math.Clamp(_options.CleanupIntervalHours, 1, DataRetentionOptions.MaxCleanupIntervalHours));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var cleanup = scope.ServiceProvider.GetRequiredService<IDataRetentionCleanupService>();
                await cleanup.RunOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Periyodik veri saklama temizliği döngüsü hata verdi.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
