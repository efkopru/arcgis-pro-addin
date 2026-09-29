namespace MultipleLeaders;

// Independent of Pro so cancellation and duplicate clicks can be checked
// without a desktop session. The service rechecks Token before changing the map.
internal sealed class PlacementLifetime
{
    private readonly CancellationTokenSource _cancellation = new();
    private int _placementClaimed;

    public CancellationToken Token => _cancellation.Token;
    public void Cancel() => _cancellation.Cancel();

    public bool TryClaimPlacement() =>
        !Token.IsCancellationRequested && Interlocked.CompareExchange(ref _placementClaimed, 1, 0) == 0;
}
