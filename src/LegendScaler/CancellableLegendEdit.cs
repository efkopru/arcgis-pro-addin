namespace LegendScaler;

/// <summary>Runs inside one native composite operation, restoring an unfinished edit.</summary>
internal static class CancellableLegendEdit
{
    public static void Run(Action change, Action restore, CancellationToken cancellationToken)
    {
        // A canceled queued action must never start a mutation or a restoration.
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            change();
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (Exception changeError)
        {
            try
            {
                // Restoration must finish even though cancellation was requested.
                restore();
            }
            catch (Exception restoreError)
            {
                throw new InvalidOperationException(
                    $"Scaling could not finish: {changeError.Message}\nRestoration also failed: {restoreError.Message}\nUse layout Undo and inspect the legend before saving.",
                    new AggregateException(changeError, restoreError));
            }
            throw;
        }
    }
}
