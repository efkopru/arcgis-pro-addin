using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ProMessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace MultipleLeaders;

internal sealed class PlacementRequest(MapView view)
{
    public MapView View { get; } = view;
    public PlacementLifetime Lifetime { get; } = new();
    public SelectionSnapshot? Selection { get; set; }
    public CalloutOptions? Options { get; set; }
}

internal static class CalloutPlacement
{
    public const string ToolId = "MultipleLeaders_PlaceCalloutTool";
    public static PlacementRequest? Current { get; private set; }

    public static PlacementRequest Begin(MapView view)
    {
        CancelCurrent();
        return Current = new PlacementRequest(view);
    }

    public static void CancelCurrent()
    {
        var previous = Current;
        Current = null;
        previous?.Lifetime.Cancel();
    }

    public static async Task StopAsync(PlacementRequest? expected = null)
    {
        // A late continuation from an old request must not cancel a newer one.
        if (expected is not null && !ReferenceEquals(Current, expected)) return;
        CancelCurrent();
        if (FrameworkApplication.CurrentTool == ToolId)
        {
            try { await FrameworkApplication.SetCurrentToolAsync("esri_mapping_exploreTool"); }
            catch (Exception ex)
            {
                // The request is already canceled even if Pro cannot switch tools.
                ProMessageBox.Show("Placement was canceled. Select Explore to change the cursor.\n" + ex.Message,
                    "Cancel Placement");
            }
        }
    }
}

internal sealed class CreateCalloutButton : Button
{
    protected override void OnUpdate() =>
        Enabled = CalloutPlacement.Current is null && MapView.Active is not null &&
            FrameworkApplication.CurrentTool != CalloutPlacement.ToolId;

    protected override async void OnClick()
    {
        if (CalloutPlacement.Current is not null || MapView.Active is not { } view ||
            FrameworkApplication.CurrentTool == CalloutPlacement.ToolId) return;
        var request = CalloutPlacement.Begin(view);
        var token = request.Lifetime.Token;
        var activated = false;
        try
        {
            request.Selection = await QueuedTask.Run(() => CalloutService.Capture(view, token));
            token.ThrowIfCancellationRequested();
            if (!ReferenceEquals(view, MapView.Active))
                throw new InvalidOperationException("The active map changed. Select points in the map you want to label.");
            var window = new CalloutOptionsWindow(request.Selection, token)
            {
                Owner = FrameworkApplication.Current.MainWindow
            };
            if (window.ShowDialog() != true) return;
            token.ThrowIfCancellationRequested();
            request.Options = window.Options;
            await FrameworkApplication.SetCurrentToolAsync(CalloutPlacement.ToolId);
            token.ThrowIfCancellationRequested();
            activated = FrameworkApplication.CurrentTool == CalloutPlacement.ToolId;
            if (!activated) throw new InvalidOperationException("Could not start label placement. Activate the source map and try again.");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            ProMessageBox.Show(ex.Message, "Create Shared Label");
        }
        finally
        {
            if (!activated) await CalloutPlacement.StopAsync(request);
        }
    }
}

internal sealed class CancelPlacementButton : Button
{
    protected override void OnUpdate() => Enabled = CalloutPlacement.Current is not null ||
        FrameworkApplication.CurrentTool == CalloutPlacement.ToolId;

    protected override async void OnClick()
    {
        try { await CalloutPlacement.StopAsync(); }
        catch (Exception ex) { ProMessageBox.Show(ex.Message, "Cancel Placement"); }
    }
}
