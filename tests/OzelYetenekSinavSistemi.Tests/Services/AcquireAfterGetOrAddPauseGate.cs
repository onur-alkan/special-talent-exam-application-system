namespace OzelYetenekSinavSistemi.Tests.Services;

/// <summary>
/// GetOrAdd ile TryAddParticipant arasındaki pencereyi deterministik test için açar.
/// </summary>
internal sealed class AcquireAfterGetOrAddPauseGate
{
    private int _getOrAddCount;
    private readonly int _pauseOnGetOrAddIndex;
    private readonly TaskCompletionSource _paused = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _resume = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public AcquireAfterGetOrAddPauseGate(int pauseOnGetOrAddIndex = 2)
        => _pauseOnGetOrAddIndex = pauseOnGetOrAddIndex;

    public ValueTask MaybePauseAsync()
    {
        if (Interlocked.Increment(ref _getOrAddCount) != _pauseOnGetOrAddIndex)
            return ValueTask.CompletedTask;

        _paused.TrySetResult();
        return new ValueTask(_resume.Task);
    }

    public Task WaitUntilPausedAsync() => _paused.Task.WaitAsync(TimeSpan.FromSeconds(5));

    public void Resume() => _resume.TrySetResult();
}
