using System.Collections.Concurrent;
using OzelYetenekSinavSistemi.Application.Interfaces.Infrastructure;

namespace OzelYetenekSinavSistemi.Infrastructure.Services;

/// <summary>
/// Process içi keyed async mutex. Çoklu instance dağıtık kilit gerektirmez.
/// Participant sayacı ve retired durumu, stale entry yarışını engeller.
/// </summary>
public sealed class KeyedAsyncLock : IKeyedAsyncLock
{
    private readonly ConcurrentDictionary<string, LockEntry> _entries = new(StringComparer.Ordinal);
    private readonly Func<ValueTask>? _afterGetOrAddHook;

    public KeyedAsyncLock()
    {
    }

    internal KeyedAsyncLock(Func<ValueTask> afterGetOrAddHook)
    {
        _afterGetOrAddHook = afterGetOrAddHook
            ?? throw new ArgumentNullException(nameof(afterGetOrAddHook));
    }

    /// <summary>Test ve tanılama için aktif sözlük girişi sayısı.</summary>
    internal int EntryCount => _entries.Count;

    /// <summary>Test için: anahtarın aktif participant sayısı (sahip + bekleyen).</summary>
    internal int GetParticipantCount(string key)
        => _entries.TryGetValue(key, out var entry) ? entry.ParticipantCount : 0;

    public async ValueTask<IAsyncDisposable> AcquireAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        while (true)
        {
            var entry = _entries.GetOrAdd(key, static _ => new LockEntry());

            if (_afterGetOrAddHook is not null)
                await _afterGetOrAddHook().ConfigureAwait(false);

            if (!entry.TryAddParticipant())
                continue;

            try
            {
                await entry.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                entry.RemoveParticipantAndTryCleanup(key, _entries);
                throw;
            }

            return new Releaser(key, entry, _entries);
        }
    }

    internal sealed class LockEntry
    {
        private readonly object _sync = new();
        private int _participantCount;
        private bool _retired;

        public SemaphoreSlim Semaphore { get; } = new(1, 1);

        internal int ParticipantCount
        {
            get
            {
                lock (_sync)
                    return _participantCount;
            }
        }

        internal bool IsRetired
        {
            get
            {
                lock (_sync)
                    return _retired;
            }
        }

        internal bool TryAddParticipant()
        {
            lock (_sync)
            {
                if (_retired)
                    return false;

                _participantCount++;
                return true;
            }
        }

        internal void RemoveParticipantAndTryCleanup(string key, ConcurrentDictionary<string, LockEntry> entries)
        {
            bool shouldRemove;
            lock (_sync)
            {
                _participantCount--;
                if (_participantCount == 0)
                {
                    _retired = true;
                    shouldRemove = true;
                }
                else
                {
                    shouldRemove = false;
                }
            }

            if (shouldRemove)
                entries.TryRemove(new KeyValuePair<string, LockEntry>(key, this));
        }
    }

    private sealed class Releaser : IAsyncDisposable
    {
        private readonly string _key;
        private readonly LockEntry _entry;
        private readonly ConcurrentDictionary<string, LockEntry> _entries;
        private int _released;

        public Releaser(string key, LockEntry entry, ConcurrentDictionary<string, LockEntry> entries)
        {
            _key = key;
            _entry = entry;
            _entries = entries;
        }

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                _entry.Semaphore.Release();
                _entry.RemoveParticipantAndTryCleanup(_key, _entries);
            }

            return ValueTask.CompletedTask;
        }
    }
}
