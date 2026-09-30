using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ProMessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace MultipleLeaders;

internal enum CalloutEditAction { Move, Resize, Reconnect }

internal sealed class ManageCalloutsButton : Button
{
    protected override void OnUpdate() => Enabled = CalloutPlacement.CanStart;
    protected override async void OnClick() => await CalloutEditWorkflow.RunAsync();
}

internal sealed class MoveCalloutButton : Button
{
    protected override void OnUpdate() => Enabled = CalloutPlacement.CanStart;
    protected override async void OnClick() => await CalloutEditWorkflow.RunAsync(CalloutEditAction.Move);
}

internal sealed class ResizeCalloutButton : Button
{
    protected override void OnUpdate() => Enabled = CalloutPlacement.CanStart;
    protected override async void OnClick() => await CalloutEditWorkflow.RunAsync(CalloutEditAction.Resize);
}

internal static class CalloutEditWorkflow
{
    public static async Task RunAsync(CalloutEditAction? action = null)
    {
        if (!CalloutPlacement.CanStart || MapView.Active is not { } view) return;
        var request = CalloutPlacement.Begin(view);
        var token = request.Lifetime.Token;
        var activated = false;
        try
        {
            var selection = action.HasValue
                ? await QueuedTask.Run(() => CalloutService.TryCaptureSelected(view, token)) : null;
            token.ThrowIfCancellationRequested();
            EnsureActiveView(view);
            if (selection is null)
            {
                var manager = new CalloutManagerWindow(view, token, action)
                {
                    Owner = FrameworkApplication.Current.MainWindow
                };
                manager.ShowDialog();
                token.ThrowIfCancellationRequested();
                selection = manager.MoveTarget;
                if (selection is null) return;
                action = CalloutEditAction.Move;
            }

            EnsureActiveView(view);
            if (action == CalloutEditAction.Move)
            {
                await QueuedTask.Run(() => CalloutService.ValidateMove(view, selection, token));
                token.ThrowIfCancellationRequested();
                EnsureActiveView(view);
                request.Editing = selection;
                await FrameworkApplication.SetCurrentToolAsync(CalloutPlacement.ToolId);
                token.ThrowIfCancellationRequested();
                if (FrameworkApplication.CurrentTool != CalloutPlacement.ToolId)
                    throw new InvalidOperationException("Could not start moving the label. Activate the source map and try again.");
                CalloutPlacement.AttachEscape(request);
                activated = true;
            }
            else if (action == CalloutEditAction.Resize)
            {
                var window = new ResizeCalloutWindow(selection.FontSize)
                {
                    Owner = FrameworkApplication.Current.MainWindow
                };
                if (window.ShowDialog() != true) return;
                token.ThrowIfCancellationRequested();
                var size = window.SelectedFontSize;
                await QueuedTask.Run(() => CalloutService.Resize(view, selection, size, token));
                Notify("Label resized", "Text size changed. Label position and leader endpoints were preserved.");
            }
            else
            {
                await QueuedTask.Run(() => CalloutService.Refresh(view, selection, token));
                Notify("Leaders reconnected", "Leader endpoints now match the original source points. Existing text and formatting were preserved.");
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { ProMessageBox.Show(error.Message, "Multiple Leaders"); }
        finally
        {
            if (!activated) CalloutPlacement.RequestStop(request);
        }
    }

    internal static void EnsureActiveView(MapView view)
    {
        if (!ReferenceEquals(view, MapView.Active))
            throw new InvalidOperationException("The active map changed. Open Manage Labels again in the map containing the label.");
    }

    internal static void Notify(string title, string message, bool isError = false)
    {
        // Reporting must not turn a successfully committed edit into an apparent
        // failure, or discard the original recovery error during application shutdown.
        try
        {
            var notification = isError
                ? new Notification(Notification.NotificationLevel.Project, NotificationType.Error)
                : new Notification();
            notification.Title = title;
            notification.Message = message;
            notification.Severity = isError ? Notification.SeverityLevel.High : Notification.SeverityLevel.Low;
            FrameworkApplication.AddNotification(notification);
        }
        catch (Exception notificationError)
        {
            System.Diagnostics.Trace.TraceError($"Multiple Leaders notification failed: {notificationError}");
            try { ProMessageBox.Show(message, title); }
            catch (Exception displayError)
            {
                System.Diagnostics.Trace.TraceError(
                    $"Multiple Leaders message delivery failed: {displayError}\nOriginal outcome: {title}: {message}");
            }
        }
    }
}
