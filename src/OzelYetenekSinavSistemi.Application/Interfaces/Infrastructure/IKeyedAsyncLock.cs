namespace OzelYetenekSinavSistemi.Application.Interfaces.Infrastructure;

/// <summary>
/// İşlem anahtarına göre async mutex. Tek instance dağıtık kilit gerektirmeyen senaryolar için.
/// </summary>
public interface IKeyedAsyncLock
{
    ValueTask<IAsyncDisposable> AcquireAsync(string key, CancellationToken cancellationToken = default);
}
