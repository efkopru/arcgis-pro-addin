using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ArcGIS.Desktop.Framework.Controls;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ProMessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace LegendScaler;

internal partial class ScaleLegendWindow : ProWindow
{
    private readonly LayoutView _view;
    private readonly LegendTarget _target;
    private readonly CancellationTokenSource _operationCancellation = new();
    private bool _ready;
    private bool _busy;
    private bool _closed;

    public ScaleLegendWindow(LayoutView view, LegendTarget target)
    {
        _view = view;
        _target = target;
        InitializeComponent();
        SelectionText.Text = $"Selected legend: {target.Name} ({target.ItemCount} items)";
        if (target.Warnings.Count > 0)
            ScopeText.Text += "\n\n" + string.Join("\n", target.Warnings);
        _ready = true;
        UpdateInput();
        Loaded += (_, _) => { PercentBox.Focus(); PercentBox.SelectAll(); };
    }

    private void PercentChanged(object sender, TextChangedEventArgs e)
    {
        if (_ready) UpdateInput();
    }

    private bool TryFactor(out double factor)
    {
        var valid = double.TryParse(PercentBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var percent)
                    && double.IsFinite(percent) && percent >= 10 && percent <= 1000;
        factor = valid ? percent / 100 : 0;
        return valid;
    }

    private void UpdateInput()
    {
        if (_closed) return;
        var valid = TryFactor(out var factor);
        CopyButton.IsEnabled = valid && !_busy;
        ApplyButton.IsEnabled = valid && !_busy && Math.Abs(factor - 1) > 1e-12;
        ErrorText.Text = valid ? "" : "Enter a percentage between 10 and 1,000.";
        DimensionsText.Text = valid
            ? $"Frame: {_target.Width:0.###} × {_target.Height:0.###} → {_target.Width * factor:0.###} × {_target.Height * factor:0.###} (layout units)"
            : "";
    }

    private async void CopyClick(object sender, RoutedEventArgs e) => await ExecuteAsync(true);
    private async void ApplyClick(object sender, RoutedEventArgs e) => await ExecuteAsync(false);
    private void CloseClick(object sender, RoutedEventArgs e) => Close();

    private async Task ExecuteAsync(bool createCopy)
    {
        if (_busy || _closed || !TryFactor(out var factor)) return;
        var cancellationToken = _operationCancellation.Token;
        _busy = true;
        UpdateInput();
        PercentBox.IsEnabled = false;
        StatusText.Text = "Applying the scale. Close or Esc cancels unfinished work.";
        try
        {
            var result = await QueuedTask.Run(() =>
                LegendScaleService.Apply(_view, _target, factor, createCopy, cancellationToken));
            if (_closed || cancellationToken.IsCancellationRequested) return;
            if (!result.FitsFrame)
                ProMessageBox.Show("The scaled legend does not fit its frame. Close this message, then enlarge its frame or use layout Undo.", "Legend Scaler");
            DialogResult = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Closing the dialog is an intentional cancellation, not a user-facing error.
        }
        catch (Exception ex)
        {
            if (_closed)
                System.Diagnostics.Trace.TraceError($"Legend Scaler operation failed after the window closed: {ex}");
            else
                ErrorText.Text = ex.Message;
        }
        finally
        {
            _busy = false;
            if (!_closed)
            {
                PercentBox.IsEnabled = true;
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
