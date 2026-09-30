using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Controls;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;

namespace LegendScaler;

internal partial class ScaleLegendWindow : ProWindow
{
    private readonly LayoutView _view;
    private readonly LegendTarget _target;
    private readonly CancellationTokenSource _operationCancellation = new();
    private bool _ready;
    private bool _busy;
    private bool _closed;

    public ScaleResult? CompletedResult { get; private set; }
    public double AppliedPercent { get; private set; }

    public ScaleLegendWindow(LayoutView view, LegendTarget target)
    {
        _view = view;
        _target = target;
        InitializeComponent();
        SelectionText.Text = $"Selected legend: {target.Name} ({target.ItemCount} items)";
        if (target.Warnings.Count > 0)
        {
            ScopeText.Text += "\n\n" + string.Join("\n", target.Warnings);
            ScopeExpander.Header = $"What to check ({target.Warnings.Count} style notes)";
        }
        _ready = true;
        UpdateInput();
        Loaded += (_, _) => { PercentBox.Focus(); PercentBox.SelectAll(); };
    }

    private void PercentChanged(object sender, TextChangedEventArgs e)
    {
        if (_ready) UpdateInput();
    }

    private void DestinationChanged(object sender, RoutedEventArgs e)
    {
        if (_ready) UpdateInput();
    }

    private void PresetClick(object sender, RoutedEventArgs e)
    {
        if (_busy || _closed || sender is not Button button) return;
        PercentBox.Text = (string)button.Tag;
        PercentBox.Focus();
        PercentBox.SelectAll();
    }

    private void UpdateInput()
    {
        if (_closed) return;
        var valid = LegendScaleInput.TryParse(PercentBox.Text, CultureInfo.CurrentCulture, out var input);
        var createCopy = CopyOption.IsChecked == true;
        ApplyButton.IsEnabled = valid && !_busy && (createCopy || input.ChangesSize);
        ApplyButton.Content = createCopy ? "Create scaled copy" : "Resize selected legend";
        ErrorText.Text = valid ? "" : "Enter a number from 10 to 1,000, such as 75 or 125%.";
        ChangeText.Text = valid ? input.DescribeChange(CultureInfo.CurrentCulture) : "";
        DimensionsText.Text = valid
            ? $"Frame (width × height, {_target.UnitName}):\n{_target.Width:0.###} × {_target.Height:0.###} → {_target.Width * input.Factor:0.###} × {_target.Height * input.Factor:0.###}"
            : "";
        DestinationText.Text = createCopy
            ? "The copy appears to the right of the original and may extend beyond the page."
            : "The selected legend changes in place, keeping its existing anchor.";
    }

    private async void ApplyClick(object sender, RoutedEventArgs e) => await ExecuteAsync(CopyOption.IsChecked == true);
    private void CloseClick(object sender, RoutedEventArgs e) => Close();

    private async Task ExecuteAsync(bool createCopy)
    {
        if (_busy || _closed || !LegendScaleInput.TryParse(PercentBox.Text, CultureInfo.CurrentCulture, out var input)
            || (!createCopy && !input.ChangesSize)) return;
        var cancellationToken = _operationCancellation.Token;
        _busy = true;
        UpdateInput();
        InputPanel.IsEnabled = false;
        StatusText.Text = "Applying the scale. Close or Esc cancels unfinished work.";
        try
        {
            var result = await QueuedTask.Run(() =>
                LegendScaleService.Apply(_view, _target, input.Factor, createCopy, cancellationToken));
            if (_closed || cancellationToken.IsCancellationRequested) return;
            CompletedResult = result;
            AppliedPercent = input.Percent;
            DialogResult = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Closing the dialog is an intentional cancellation, not a user-facing error.
        }
        catch (Exception ex)
        {
            if (_closed)
            {
                System.Diagnostics.Trace.TraceError($"Legend Scaler operation failed after the window closed: {ex}");
                LegendNotifications.Show(new Notification(Notification.NotificationLevel.Project, NotificationType.Error)
                {
                    Severity = Notification.SeverityLevel.High,
                    Title = "Legend scaling could not finish",
                    Message = "The resize dialog is closed, but the operation reported an error. " + ex.Message
                });
            }
            else
                ErrorText.Text = ex.Message;
        }
        finally
        {
            _busy = false;
            if (!_closed)
            {
                InputPanel.IsEnabled = true;
                StatusText.Text = "";
                var error = ErrorText.Text;
                UpdateInput();
                if (!string.IsNullOrEmpty(error)) ErrorText.Text = error;
            }
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Never prevent exit while the Pro worker queue is busy. The worker checks
        // cancellation before edits and restores an unfinished edit when possible.
        _operationCancellation.Cancel();
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _closed = true;
        _operationCancellation.Dispose();
        base.OnClosed(e);
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
            return;
        }
        base.OnPreviewKeyDown(e);
    }
}
