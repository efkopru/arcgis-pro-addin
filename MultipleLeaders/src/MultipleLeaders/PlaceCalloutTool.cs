using System.Windows.Input;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ProMessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace MultipleLeaders;

internal sealed class PlaceCalloutTool : MapTool
{
    public PlaceCalloutTool()
    {
        IsSketchTool = true;
        SketchType = SketchGeometryType.Point;
        SketchOutputMode = SketchOutputMode.Map;
        UseSnapping = false;
        SketchTip = "Click once to place the shared label. Esc cancels.";
    }

    protected override Task OnToolActivateAsync(bool hasMapViewChanged)
    {
        SketchTip = CalloutPlacement.Current?.Editing is not null
            ? "Click the label's new position. Esc cancels the move."
            : "Click once to place the shared label. Esc cancels.";
        return Task.CompletedTask;
    }

    protected override void OnToolKeyDown(MapViewKeyEventArgs args)
    {
        using var callback = CalloutPlacement.EnterToolCallback();
        if (args.Key != Key.Escape) return;
        args.Handled = true;
        CalloutPlacement.RequestStop();
    }

    protected override Task HandleKeyDownAsync(MapViewKeyEventArgs args) => Task.CompletedTask;

    protected override Task<bool> OnSketchCanceledAsync()
    {
        using var callback = CalloutPlacement.EnterToolCallback();
        CalloutPlacement.RequestStop();
        return Task.FromResult(true);
    }

    protected override Task OnToolDeactivateAsync(bool hasMapViewChanged)
    {
        // Deactivation also runs when the user intentionally switches tools/maps.
        // Do not select another tool from this callback.
        CalloutPlacement.CancelCurrent();
        return Task.CompletedTask;
    }

    protected override async Task<bool> OnSketchCompleteAsync(Geometry geometry)
    {
        using var callback = CalloutPlacement.EnterToolCallback();
        var request = CalloutPlacement.Current;
        if (request is null || !request.IsReady || !request.Lifetime.TryClaimPlacement()) return true;
        var token = request.Lifetime.Token;
        try
        {
            token.ThrowIfCancellationRequested();
            if (!ReferenceEquals(request.View, MapView.Active) || geometry is not MapPoint location)
                throw new InvalidOperationException("The active map changed. Return to the source map and start the label command again.");
            if (request.Editing is { } editing)
            {
                await QueuedTask.Run(() => CalloutService.Move(request.View, editing, location, token));
            }
            else
            {
                var options = request.Options ?? throw new InvalidOperationException("Enter label text before placing the label.");
                var selection = request.Selection ?? throw new InvalidOperationException("Select the source points first.");
                await QueuedTask.Run(() => CalloutService.Create(request.View, selection, location,
                    options.Text, options.FontSize, options.LineWidth, options.SourceField, token));
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            ProMessageBox.Show(ex.Message, request.Editing is null ? "Create Shared Label" : "Move Label");
        }
        finally
        {
            CalloutPlacement.RequestStop(request);
        }
        return true;
    }
}
