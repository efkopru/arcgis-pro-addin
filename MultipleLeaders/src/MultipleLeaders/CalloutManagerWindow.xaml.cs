using System.Threading;
using System.Windows;
using System.Windows.Controls;
using ArcGIS.Desktop.Framework.Controls;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace MultipleLeaders;

internal partial class CalloutManagerWindow : ProWindow
{
    private readonly MapView _view;
    private readonly CancellationTokenSource _lifetime;
    private readonly CancellationToken _token;
    private readonly CancellationTokenRegistration _cancellation;
    private bool _closed;
    private bool _busy;
    public EditableCalloutSnapshot? MoveTarget { get; private set; }

    public CalloutManagerWindow(MapView view, CancellationToken parentToken, CalloutEditAction? preferredAction)
    {
        _view = view;
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(parentToken);
        _token = _lifetime.Token;
        InitializeComponent();
        if (preferredAction.HasValue)
            InstructionText.Text = "No single editable shared label is selected. Choose the label below, then choose the action you want.";
        _cancellation = _token.Register(() => Dispatcher.BeginInvoke(new Action(() => { if (!_closed) Close(); })));
        Loaded += async (_, _) => await LoadAsync();
        Closed += (_, _) =>
        {
            _closed = true;
            _lifetime.Cancel();
            _cancellation.Dispose();
            _lifetime.Dispose();
        };
    }

    private async Task LoadAsync(EditableCalloutSnapshot? selectedTarget = null, string? completion = null)
    {
        SetBusy(true, "Reading shared labels...");
        try
        {
            var labels = await QueuedTask.Run(() => CalloutService.ListLabels(_view, _token));
            if (_closed) return;
            _token.ThrowIfCancellationRequested();
            CalloutEditWorkflow.EnsureActiveView(_view);
            LabelsGrid.ItemsSource = labels;
            // Never choose an arbitrary row when several unselected labels exist.
            var originallySelected = labels.Where(item => item.IsSelected).ToArray();
            LabelsGrid.SelectedItem = (selectedTarget is null ? null : labels.FirstOrDefault(item =>
                    item.Snapshot is { } candidate && Equals(candidate.Layer, selectedTarget.Layer) &&
                    Equals(candidate.Element, selectedTarget.Element)))
                ?? (originallySelected.Length == 1 ? originallySelected[0] : labels.Count == 1 ? labels[0] : null);
            StatusText.Text = completion ?? (labels.Count == 0
                ? "No shared labels exist in this map. Close this window, select 2–100 point features, and use Create Shared Label."
                : $"{labels.Count} shared label(s) in this map. Reconnect updates endpoints; it does not refresh text from a field.");
        }
        catch (OperationCanceledException) when (_token.IsCancellationRequested) { }
        catch (Exception error)
        {
            if (!_closed)
            {
                LabelsGrid.ItemsSource = null;
                StatusText.Text = completion ?? "The label list could not be loaded.";
                ErrorText.Text = error.Message;
            }
        }
        finally { if (!_closed) SetBusy(false); }
    }

    private void SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateActions();

    private void UpdateActions()
    {
        if (MoveButton is null) return;
        var item = LabelsGrid.SelectedItem as CalloutListItem;
        ResizeButton.IsEnabled = !_busy && item?.CanEdit == true;
        MoveButton.IsEnabled = ReconnectButton.IsEnabled = !_busy && item?.CanReconnect == true;
        SelectionStatus.Text = item is null ? "Select one row to enable editing." : item.Status;
    }

    private void SetBusy(bool busy, string? message = null)
    {
        _busy = busy;
        LabelsGrid.IsEnabled = !busy;
        if (message is not null) { StatusText.Text = message; ErrorText.Text = string.Empty; }
        UpdateActions();
    }

    private async void MoveClick(object sender, RoutedEventArgs e)
    {
        if (_busy || (LabelsGrid.SelectedItem as CalloutListItem)?.Snapshot is not { } snapshot) return;
        SetBusy(true, "Preparing the label for movement...");
        try
        {
            await QueuedTask.Run(() => CalloutService.ValidateMove(_view, snapshot, _token));
            if (_closed) return;
            _token.ThrowIfCancellationRequested();
            CalloutEditWorkflow.EnsureActiveView(_view);
            MoveTarget = snapshot;
            DialogResult = true;
        }
        catch (OperationCanceledException) when (_token.IsCancellationRequested) { }
        catch (Exception error) { ShowError(error); }
        finally { if (!_closed) SetBusy(false); }
    }

    private async void ResizeClick(object sender, RoutedEventArgs e)
    {
        if (_busy || (LabelsGrid.SelectedItem as CalloutListItem)?.Snapshot is not { } snapshot) return;
        var window = new ResizeCalloutWindow(snapshot.FontSize) { Owner = this };
        if (window.ShowDialog() != true || _closed) return;
        var fontSize = window.SelectedFontSize;
        await EditAsync(snapshot, () => CalloutService.Resize(_view, snapshot, fontSize, _token),
            "Resizing label...", "Label resized. Position, endpoints, and leader widths were preserved.");
    }

    private async void ReconnectClick(object sender, RoutedEventArgs e)
    {
        if (_busy || (LabelsGrid.SelectedItem as CalloutListItem)?.Snapshot is not { } snapshot) return;
        await EditAsync(snapshot, () => CalloutService.Refresh(_view, snapshot, _token),
            "Reconnecting leaders...", "Leaders reconnected to the original source points. Existing text and styling were preserved.");
    }

    private async Task EditAsync(EditableCalloutSnapshot snapshot, Func<string> edit, string progress, string completion)
    {
        SetBusy(true, progress + " Close or Esc cancels unfinished work.");
        try
        {
            await QueuedTask.Run(edit);
            if (_closed) return;
            await LoadAsync(snapshot, completion);
        }
        catch (OperationCanceledException) when (_token.IsCancellationRequested) { }
        catch (Exception error) { ShowError(error); }
        finally { if (!_closed) SetBusy(false); }
    }

    private void ShowError(Exception error)
    {
        if (_closed)
        {
            // Closing may race a native mutation and its rollback. Suppress expected
            // cancellation, but never hide a failed restoration that needs attention.
            CalloutEditWorkflow.Notify("Shared-label action failed", error.Message, isError: true);
            return;
        }
        StatusText.Text = "The action did not complete. Review the message below.";
        ErrorText.Text = error.Message;
    }
}
