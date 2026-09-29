using System.Windows.Input;
using System.Windows.Threading;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ProMessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace MultipleLeaders;

internal sealed class PlacementRequest(MapView view, long revision)
{
    public MapView View { get; } = view;
    public long Revision { get; } = revision;
    public PlacementLifetime Lifetime { get; } = new();
    public SelectionSnapshot? Selection { get; set; }
    public CalloutOptions? Options { get; set; }
}

internal static class CalloutPlacement
{
    public const string ToolId = "MultipleLeaders_PlaceCalloutTool";
    public static PlacementRequest? Current { get; private set; }
    private static InputManager? _inputManager;
    private static PlacementRequest? _escapeRequest;
    private static readonly PlacementExitScheduler ExitScheduler = new(
        action => FrameworkApplication.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, action),
        CanExitTool,
        () => FrameworkApplication.SetCurrentToolAsync("esri_mapping_exploreTool"),
        ex => ProMessageBox.Show("Placement was canceled. Select Explore to change the cursor.\n" + ex.Message,
            "Cancel Placement"));

    public static bool IsExiting => ExitScheduler.IsBusy;
    public static IDisposable EnterToolCallback() => ExitScheduler.EnterCallback();

    public static PlacementRequest Begin(MapView view)
    {
        var revision = ExitScheduler.BeginRequest();
        CancelCurrentCore();
        return Current = new PlacementRequest(view, revision);
    }

    public static void CancelCurrent()
    {
        CancelCurrentCore();
        ExitScheduler.Invalidate();
    }

    private static void CancelCurrentCore()
    {
        DetachEscape();
        var previous = Current;
        Current = null;
        previous?.Lifetime.Cancel();
    }

    public static void RequestStop(PlacementRequest? expected = null)
    {
        // A late continuation must not cancel a newer request. A previously canceled
        // request may still finish activating the tool; let its same-revision finally
        // schedule another exit after activation, even though Current is already null.
        if (expected is not null && (expected.Revision != ExitScheduler.Revision ||
            (Current is not null && !ReferenceEquals(Current, expected)))) return;
        var revision = expected?.Revision ?? Current?.Revision ?? ExitScheduler.Revision;
        CancelCurrentCore();
        ExitScheduler.RequestExit(revision);
    }

    private static bool CanExitTool()
    {
        if (Current is not null || FrameworkApplication.CurrentTool != ToolId) return false;
        // Respect a different tool the user has already started switching to.
        return FrameworkApplication.OutgoingTool != ToolId ||
            string.IsNullOrEmpty(FrameworkApplication.IncomingTool) ||
            FrameworkApplication.IncomingTool == ToolId;
    }

    public static void AttachEscape(PlacementRequest request)
    {
        DetachEscape();
        if (!ReferenceEquals(Current, request) || request.Options is null ||
            request.Lifetime.Token.IsCancellationRequested || FrameworkApplication.CurrentTool != ToolId)
            return;
        _escapeRequest = request;
        _inputManager = InputManager.Current;
        _inputManager.PreProcessInput += OnPreProcessInput;
    }

    private static void DetachEscape()
    {
        if (_inputManager is not null)
            _inputManager.PreProcessInput -= OnPreProcessInput;
        _inputManager = null;
        _escapeRequest = null;
    }

    private static void OnPreProcessInput(object sender, PreProcessInputEventArgs args)
    {
        var request = _escapeRequest;
        if (request is null || !ReferenceEquals(Current, request) ||
            FrameworkApplication.CurrentTool != ToolId ||
            args.StagingItem.Input is not KeyEventArgs key ||
            key.RoutedEvent != Keyboard.PreviewKeyDownEvent || key.Key != Key.Escape)
            return;

        // InputManager also sees the sketch-tip popup's presentation source.
        // Cancel only this Escape event during our accepted placement session.
        key.Handled = true;
        args.Cancel();
        RequestStop(request);
    }
}

internal sealed class CreateCalloutButton : Button
{
    protected override void OnUpdate() =>
        Enabled = !CalloutPlacement.IsExiting && CalloutPlacement.Current is null && MapView.Active is not null &&
            FrameworkApplication.CurrentTool != CalloutPlacement.ToolId;

    protected override async void OnClick()
    {
        if (CalloutPlacement.IsExiting || CalloutPlacement.Current is not null || MapView.Active is not { } view ||
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
            if (FrameworkApplication.CurrentTool != CalloutPlacement.ToolId)
                throw new InvalidOperationException("Could not start label placement. Activate the source map and try again.");
            CalloutPlacement.AttachEscape(request);
            activated = true;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            ProMessageBox.Show(ex.Message, "Create Shared Label");
        }
        finally
        {
            if (!activated) CalloutPlacement.RequestStop(request);
        }
    }
}

internal sealed class CancelPlacementButton : Button
{
    protected override void OnUpdate() => Enabled = CalloutPlacement.Current is not null ||
        FrameworkApplication.CurrentTool == CalloutPlacement.ToolId;

    protected override void OnClick()
    {
        try { CalloutPlacement.RequestStop(); }
        catch (Exception ex) { ProMessageBox.Show(ex.Message, "Cancel Placement"); }
    }
}
