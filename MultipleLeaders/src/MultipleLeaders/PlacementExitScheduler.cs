namespace MultipleLeaders;

/// <summary>
/// UI-thread coordinator. Tool transitions are posted only after all MapTool
/// callbacks have unwound, and are never tasks returned to those callbacks.
/// The injected delegates keep the scheduling/race behavior testable without Pro.
/// </summary>
internal sealed class PlacementExitScheduler(
    Action<Action> post,
    Func<bool> canExit,
    Func<Task> exitTool,
    Action<Exception> reportError)
{
    private long _revision;
    private long? _pending;
    private long? _posted;
    private int _callbacks;
    private bool _switching;

    public long Revision => _revision;
    public bool IsBusy => _pending.HasValue || _posted == _revision || _switching;

    public long BeginRequest()
    {
        if (_switching)
            throw new InvalidOperationException("Wait for the current tool transition to finish.");
        Invalidate();
        return _revision;
    }

    public void Invalidate()
    {
        _revision++;
        _pending = null;
        // An already-running transition still owns _switching until its finally.
    }

    public IDisposable EnterCallback()
    {
        _callbacks++;
        return new CallbackScope(this);
    }

    public void RequestExit(long revision)
    {
        if (revision != _revision || _switching) return;
        _pending = revision;
        PostIfReady();
    }

    private void LeaveCallback()
    {
        _callbacks--;
        PostIfReady();
    }

    private void PostIfReady()
    {
        if (_callbacks != 0 || _switching || _pending is not { } revision || _posted == revision)
            return;
        _posted = revision;
        try { post(() => _ = DispatchAsync(revision)); }
        catch
        {
            if (_posted == revision) _posted = null;
            if (_pending == revision) _pending = null;
            throw;
        }
    }

    private async Task DispatchAsync(long revision)
    {
        if (_posted == revision) _posted = null;
        if (revision != _revision || _pending != revision) return;
        // Another callback can start after posting and before dispatch. Its scope
        // will post again when it leaves; do not spin the dispatcher meanwhile.
        if (_callbacks != 0 || _switching) return;
        _pending = null;
        var ownsTransition = false;
        try
        {
            if (!canExit() || revision != _revision) return;
            _switching = ownsTransition = true;
            await exitTool();
        }
        catch (Exception error)
        {
            reportError(error);
        }
        finally
        {
            // Deactivation invalidates the revision during our own tool switch.
            // That must not prevent releasing this transition's busy state.
            if (ownsTransition) _switching = false;
        }
    }

    private sealed class CallbackScope(PlacementExitScheduler owner) : IDisposable
    {
        private PlacementExitScheduler? _owner = owner;

        public void Dispose()
        {
            var current = _owner;
            _owner = null;
            current?.LeaveCallback();
        }
    }
}
