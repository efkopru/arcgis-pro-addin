using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ProMessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace MultipleLeaders;

internal sealed class MoveCalloutButton : Button
{
    protected override void OnUpdate() => Enabled = CalloutPlacement.CanStart;

    protected override async void OnClick()
    {
        if (!CalloutPlacement.CanStart || MapView.Active is not { } view) return;
        var request = CalloutPlacement.Begin(view);
        var token = request.Lifetime.Token;
        var activated = false;
        try
        {
            request.Editing = await QueuedTask.Run(() => CalloutService.CaptureSelected(view, token));
            token.ThrowIfCancellationRequested();
            if (!ReferenceEquals(view, MapView.Active))
                throw new InvalidOperationException("The active map changed. Select the shared label in its source map and start Move Label again.");

            await FrameworkApplication.SetCurrentToolAsync(CalloutPlacement.ToolId);
            token.ThrowIfCancellationRequested();
            if (FrameworkApplication.CurrentTool != CalloutPlacement.ToolId)
                throw new InvalidOperationException("Could not start moving the label. Activate the source map and try again.");
            CalloutPlacement.AttachEscape(request);
            activated = true;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            ProMessageBox.Show(ex.Message, "Move Label");
        }
        finally
        {
            if (!activated) CalloutPlacement.RequestStop(request);
        }
    }
}

internal sealed class ResizeCalloutButton : Button
{
    private bool _busy;

    protected override void OnUpdate() => Enabled = !_busy && CalloutPlacement.CanStart;

    protected override async void OnClick()
    {
        if (_busy || !CalloutPlacement.CanStart || MapView.Active is not { } view) return;
        _busy = true;
        var request = CalloutPlacement.Begin(view);
        var token = request.Lifetime.Token;
        try
        {
            var selection = await QueuedTask.Run(() => CalloutService.CaptureSelected(view, token));
            token.ThrowIfCancellationRequested();
            if (!ReferenceEquals(view, MapView.Active))
                throw new InvalidOperationException("The active map changed. Select the shared label in its source map and start Resize Label again.");

            var window = new ResizeCalloutWindow(selection.FontSize)
            {
                Owner = FrameworkApplication.Current.MainWindow
            };
            if (window.ShowDialog() != true) return;
            token.ThrowIfCancellationRequested();
            if (!ReferenceEquals(view, MapView.Active))
                throw new InvalidOperationException("The active map changed. Start Resize Label again in the source map.");
            var fontSize = window.SelectedFontSize;
            await QueuedTask.Run(() => CalloutService.Resize(view, selection, fontSize, token));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            ProMessageBox.Show(ex.Message, "Resize Label");
        }
        finally
        {
            _busy = false;
            CalloutPlacement.RequestStop(request);
        }
    }
}
