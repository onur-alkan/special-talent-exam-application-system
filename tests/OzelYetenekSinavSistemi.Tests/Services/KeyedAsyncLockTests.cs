using System.Collections.Concurrent;
using OzelYetenekSinavSistemi.Application.Interfaces.Infrastructure;
using OzelYetenekSinavSistemi.Infrastructure.Services;

namespace OzelYetenekSinavSistemi.Tests.Services;

public sealed class KeyedAsyncLockTests
{
    [Fact]
    public async Task LegacyTryRemove_ControlledSequence_AllowsTwoActiveSemaphoresForSameKey()
    {
        var entries = new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.Ordinal);
        const string key = "race-key";

        var semA = entries.GetOrAdd(key, static _ => new SemaphoreSlim(1, 1));
        Assert.True(semA.Wait(0));

        var bReachedWait = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var taskB = Task.Run(() =>
        {
            var sem = entries.GetOrAdd(key, static _ => new SemaphoreSlim(1, 1));
            bReachedWait.SetResult();
            Assert.True(sem.Wait(TimeSpan.FromSeconds(5)));
        });

        await bReachedWait.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Same(semA, entries[key]);
        await WaitUntilAsync(() => !taskB.IsCompleted, TimeSpan.FromSeconds(5));

        semA.Release();
        Assert.True(entries.TryRemove(new KeyValuePair<string, SemaphoreSlim>(key, semA)));

        var semC = entries.GetOrAdd(key, static _ => new SemaphoreSlim(1, 1));
        Assert.NotSame(semA, semC);
        Assert.True(semC.Wait(0));

        await taskB.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(0, semA.CurrentCount);
        Assert.Equal(0, semC.CurrentCount);
    }

    [Fact]
    public void LockEntry_WhenRetired_TryAddParticipantReturnsFalse()
    {
        var entries = new ConcurrentDictionary<string, KeyedAsyncLock.LockEntry>(StringComparer.Ordinal);
        var entry = new KeyedAsyncLock.LockEntry();
        entries["k"] = entry;

        Assert.True(entry.TryAddParticipant());
        Assert.Equal(1, entry.ParticipantCount);

        entry.RemoveParticipantAndTryCleanup("k", entries);

        Assert.True(entry.IsRetired);
        Assert.False(entry.TryAddParticipant());
        Assert.Empty(entries);
    }

    [Fact]
    public void ProductionKeyedAsyncLock_HasNoStaticTestPauseGate()
    {
        var source = ReadInfrastructureSource("KeyedAsyncLock.cs");

        Assert.DoesNotContain("TestPauseGate", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AcquireAfterGetOrAddPauseGate", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task KeyedAsyncLock_WithoutHook_WorksNormally()
    {
        var keyedLock = new KeyedAsyncLock();

        await using (var first = await keyedLock.AcquireAsync("production"))
            Assert.Equal(1, keyedLock.EntryCount);

        Assert.Equal(0, keyedLock.EntryCount);
    }

    [Fact]
    public async Task KeyedAsyncLock_InstanceHooks_DoNotAffectOtherInstances()
    {
        var gateA = new AcquireAfterGetOrAddPauseGate(pauseOnGetOrAddIndex: 1);
        var gateB = new AcquireAfterGetOrAddPauseGate(pauseOnGetOrAddIndex: 1);
        var lockA = new KeyedAsyncLock(gateA.MaybePauseAsync);
        var lockB = new KeyedAsyncLock(gateB.MaybePauseAsync);

        var lockBTask = Task.Run(async () =>
        {
            await using var handle = await lockB.AcquireAsync("isolated-b");
        });

        await gateB.WaitUntilPausedAsync();
        Assert.False(lockBTask.IsCompleted);

        var lockATask = Task.Run(async () =>
        {
            await using var handle = await lockA.AcquireAsync("isolated-a");
        });

        await gateA.WaitUntilPausedAsync();
        Assert.False(lockBTask.IsCompleted);

        gateA.Resume();
        await lockATask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(lockBTask.IsCompleted);

        gateB.Resume();
        await lockBTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(0, lockA.EntryCount);
        Assert.Equal(0, lockB.EntryCount);
    }

    [Fact]
    public async Task KeyedAsyncLock_StaleEntryAfterGetOrAdd_BAndCNeverOverlap()
    {
        const string key = "stale-key";
        var pauseGate = new AcquireAfterGetOrAddPauseGate(pauseOnGetOrAddIndex: 2);
        var keyedLock = new KeyedAsyncLock(pauseGate.MaybePauseAsync);

        var concurrent = 0;
        var maxConcurrent = 0;

        var aHandle = await keyedLock.AcquireAsync(key);
        Assert.Equal(1, keyedLock.GetParticipantCount(key));

        var bAcquired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cAcquired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var bTask = Task.Run(async () =>
        {
            await using var handle = await keyedLock.AcquireAsync(key);
            var current = Interlocked.Increment(ref concurrent);
            UpdateMax(ref maxConcurrent, current);
            bAcquired.SetResult();
            Interlocked.Decrement(ref concurrent);
        });

        await pauseGate.WaitUntilPausedAsync();
        Assert.Equal(1, keyedLock.GetParticipantCount(key));

        await aHandle.DisposeAsync();
        Assert.Equal(0, keyedLock.EntryCount);

        var cTask = Task.Run(async () =>
        {
            await using var handle = await keyedLock.AcquireAsync(key);
            var current = Interlocked.Increment(ref concurrent);
            UpdateMax(ref maxConcurrent, current);
            cAcquired.SetResult();
            Interlocked.Decrement(ref concurrent);
        });

        await Task.Yield();

        pauseGate.Resume();

        await Task.WhenAll(
            bAcquired.Task.WaitAsync(TimeSpan.FromSeconds(5)),
            cAcquired.Task.WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.True(maxConcurrent <= 1, $"Stale entry yarışında maxConcurrent={maxConcurrent}");

        await Task.WhenAll(bTask, cTask);

        Assert.Equal(0, keyedLock.EntryCount);
    }

    [Fact]
    public async Task KeyedAsyncLock_Releaser_DoubleDispose_DoesNotDoubleReleaseSemaphore()
    {
        var keyedLock = new KeyedAsyncLock();
        const string key = "double-dispose";

        var handle = await keyedLock.AcquireAsync(key);
        await handle.DisposeAsync();
        await handle.DisposeAsync();

        await using var second = await keyedLock.AcquireAsync(key);
        Assert.NotNull(second);
        Assert.Equal(1, keyedLock.EntryCount);
    }

    [Fact]
    public async Task KeyedAsyncLock_ABC_ReleaseWhileWaiterAndNewAcquirer_NeverOverlap()
    {
        var keyedLock = new KeyedAsyncLock();

        for (var i = 0; i < 200; i++)
        {
            var (maxConcurrent, overlap) = await AbcRaceHarness.RunOnceAsync(keyedLock, $"abc-race-{i}");
            Assert.True(maxConcurrent <= 1, $"İterasyon {i}: maxConcurrent={maxConcurrent}");
            Assert.Equal(0, overlap);
        }
    }

    [Fact]
    public async Task KeyedAsyncLock_ABC_WaiterPresent_EntryNotRemovedUntilLastParticipantLeaves()
    {
        var keyedLock = new KeyedAsyncLock();
        const string key = "entry-retention";
        var aHolding = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseA = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var bAcquired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseB = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var taskA = Task.Run(async () =>
        {
            await using var handle = await keyedLock.AcquireAsync(key);
            aHolding.SetResult();
            await releaseA.Task;
        });

        await aHolding.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, keyedLock.EntryCount);

        var taskB = Task.Run(async () =>
        {
            await using var handle = await keyedLock.AcquireAsync(key);
            bAcquired.SetResult();
            await releaseB.Task;
        });

        await WaitUntilAsync(() => keyedLock.GetParticipantCount(key) >= 2, TimeSpan.FromSeconds(5));

        releaseA.SetResult();
        await taskA.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, keyedLock.EntryCount);
        Assert.True(keyedLock.GetParticipantCount(key) >= 1);

        await bAcquired.Task.WaitAsync(TimeSpan.FromSeconds(5));

        releaseB.SetResult();
        await taskB.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(0, keyedLock.EntryCount);
    }

    [Fact]
    public async Task KeyedAsyncLock_CancelWhileWaiting_DecrementsParticipantCount()
    {
        var keyedLock = new KeyedAsyncLock();
        var holderReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseHolder = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var holder = Task.Run(async () =>
        {
            await using var handle = await keyedLock.AcquireAsync("cancel-participant");
            holderReady.SetResult();
            await releaseHolder.Task;
        });

        await holderReady.Task.WaitAsync(TimeSpan.FromSeconds(5));

        using var cts = new CancellationTokenSource();
        var waiter = keyedLock.AcquireAsync("cancel-participant", cts.Token).AsTask();

        await WaitUntilAsync(() => keyedLock.GetParticipantCount("cancel-participant") >= 2, TimeSpan.FromSeconds(5));
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => waiter);
        Assert.Equal(1, keyedLock.EntryCount);
        Assert.Equal(1, keyedLock.GetParticipantCount("cancel-participant"));

        releaseHolder.SetResult();
        await holder.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(0, keyedLock.EntryCount);
    }

    [Fact]
    public async Task KeyedAsyncLock_ReleaseOnException_AllowsNextAcquireAndCleansUp()
    {
        var keyedLock = new KeyedAsyncLock();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await using var handle = await keyedLock.AcquireAsync("exc-key");
            throw new InvalidOperationException("test");
        });

        Assert.Equal(0, keyedLock.EntryCount);

        await using var second = await keyedLock.AcquireAsync("exc-key");
        Assert.NotNull(second);
        Assert.Equal(1, keyedLock.EntryCount);
    }

    [Fact]
    public async Task KeyedAsyncLock_DifferentKeys_DoNotBlockEachOther()
    {
        var keyedLock = new KeyedAsyncLock();
        var enteredA = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseA = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var enteredB = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var taskA = Task.Run(async () =>
        {
            await using var handle = await keyedLock.AcquireAsync("key-a");
            enteredA.SetResult();
            await releaseA.Task;
        });

        await enteredA.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var taskB = Task.Run(async () =>
        {
            await using var handle = await keyedLock.AcquireAsync("key-b");
            enteredB.SetResult();
        });

        await enteredB.Task.WaitAsync(TimeSpan.FromSeconds(5));
        releaseA.SetResult();
        await Task.WhenAll(taskA, taskB);
    }

    [Fact]
    public async Task KeyedAsyncLock_ManyUniqueKeys_DoNotLeaveEntriesAfterRelease()
    {
        var keyedLock = new KeyedAsyncLock();

        for (var i = 0; i < 100; i++)
        {
            await using var handle = await keyedLock.AcquireAsync($"ephemeral-key-{i}");
        }

        Assert.Equal(0, keyedLock.EntryCount);
    }

    [Fact]
    public async Task KeyedAsyncLock_ReacquireSameKeyAfterCleanup_Works()
    {
        var keyedLock = new KeyedAsyncLock();

        await using (var first = await keyedLock.AcquireAsync("reuse-key"))
            Assert.Equal(1, keyedLock.EntryCount);

        Assert.Equal(0, keyedLock.EntryCount);

        await using var second = await keyedLock.AcquireAsync("reuse-key");
        Assert.Equal(1, keyedLock.EntryCount);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return;
            await Task.Yield();
        }

        throw new TimeoutException("Beklenen koşul zaman aşımına uğradı.");
    }

    private static void UpdateMax(ref int maxConcurrent, int current)
    {
        while (true)
        {
            var max = Volatile.Read(ref maxConcurrent);
            if (current <= max) return;
            if (Interlocked.CompareExchange(ref maxConcurrent, current, max) == max) return;
        }
    }

    private static string ReadInfrastructureSource(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "OzelYetenekSinavSistemi.Infrastructure",
                "Services",
                fileName);
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);
            directory = directory.Parent;
        }

        throw new FileNotFoundException(fileName);
    }

    /// <summary>
    /// Eski TryRemove-after-Release uygulamasının kopyası; yarış kanıtı için kullanılır.
    /// </summary>
    private sealed class LegacyTryRemoveKeyedAsyncLock : IKeyedAsyncLock
    {
        private readonly ConcurrentDictionary<string, LegacyLockEntry> _entries = new(StringComparer.Ordinal);

        public async ValueTask<IAsyncDisposable> AcquireAsync(string key, CancellationToken cancellationToken = default)
        {
            var entry = _entries.GetOrAdd(key, static _ => new LegacyLockEntry());
            await entry.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new LegacyReleaser(key, entry, _entries);
        }

        private sealed class LegacyLockEntry
        {
            public SemaphoreSlim Semaphore { get; } = new(1, 1);
        }

        private sealed class LegacyReleaser : IAsyncDisposable
        {
            private readonly string _key;
            private readonly LegacyLockEntry _entry;
            private readonly ConcurrentDictionary<string, LegacyLockEntry> _entries;
            private int _released;

            public LegacyReleaser(string key, LegacyLockEntry entry, ConcurrentDictionary<string, LegacyLockEntry> entries)
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
                    _entries.TryRemove(new KeyValuePair<string, LegacyLockEntry>(_key, _entry));
                }

                return ValueTask.CompletedTask;
            }
        }
    }

    private static class AbcRaceHarness
    {
        public static async Task<(int maxConcurrent, int overlap)> RunOnceAsync(IKeyedAsyncLock keyedLock, string key)
        {
            var aHolding = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseA = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var bEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var cEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseB = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseC = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var concurrent = 0;
            var maxConcurrent = 0;
            var overlap = 0;

            void Enter()
            {
                var current = Interlocked.Increment(ref concurrent);
                UpdateMax(current);
                if (current > 1)
                    Interlocked.Increment(ref overlap);
            }

            void Exit() => Interlocked.Decrement(ref concurrent);

            void UpdateMax(int current)
            {
                while (true)
                {
                    var max = Volatile.Read(ref maxConcurrent);
                    if (current <= max) return;
                    if (Interlocked.CompareExchange(ref maxConcurrent, current, max) == max) return;
                }
            }

            var taskA = Task.Run(async () =>
            {
                await using var handle = await keyedLock.AcquireAsync(key);
                Enter();
                aHolding.SetResult();
                try
                {
                    await releaseA.Task;
                }
                finally
                {
                    Exit();
                }
            });

            await aHolding.Task.WaitAsync(TimeSpan.FromSeconds(5));

            var taskB = Task.Run(async () =>
            {
                await using var handle = await keyedLock.AcquireAsync(key);
                Enter();
                bEntered.SetResult();
                try
                {
                    await releaseB.Task;
                }
                finally
                {
                    Exit();
                }
            });

            for (var spin = 0; spin < 64 && !bEntered.Task.IsCompleted; spin++)
                await Task.Yield();

            var taskC = Task.Run(async () =>
            {
                await using var handle = await keyedLock.AcquireAsync(key);
                Enter();
                cEntered.SetResult();
                try
                {
                    await releaseC.Task;
                }
                finally
                {
                    Exit();
                }
            });

            for (var spin = 0; spin < 64; spin++)
                await Task.Yield();

            releaseA.SetResult();
            await taskA.WaitAsync(TimeSpan.FromSeconds(5));

            for (var spin = 0; spin < 16; spin++)
                await Task.Yield();

            if (bEntered.Task.IsCompleted && cEntered.Task.IsCompleted)
                Interlocked.Increment(ref overlap);

            if (bEntered.Task.IsCompleted && !cEntered.Task.IsCompleted)
            {
                releaseB.SetResult();
                await cEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                releaseC.SetResult();
            }
            else if (cEntered.Task.IsCompleted && !bEntered.Task.IsCompleted)
            {
                releaseC.SetResult();
                await bEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                releaseB.SetResult();
            }
            else if (!bEntered.Task.IsCompleted && !cEntered.Task.IsCompleted)
            {
                var first = await Task.WhenAny(bEntered.Task, cEntered.Task).WaitAsync(TimeSpan.FromSeconds(5));
                if (ReferenceEquals(first, bEntered.Task))
                {
                    releaseB.SetResult();
                    await cEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                    releaseC.SetResult();
                }
                else
                {
                    releaseC.SetResult();
                    await bEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                    releaseB.SetResult();
                }
            }
            else
            {
                releaseB.SetResult();
                releaseC.SetResult();
            }

            await Task.WhenAll(taskB, taskC);

            return (Volatile.Read(ref maxConcurrent), Volatile.Read(ref overlap));
        }
    }
}
