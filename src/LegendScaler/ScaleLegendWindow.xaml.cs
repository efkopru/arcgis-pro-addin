using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using ArcGIS.Desktop.Framework.Controls;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ProMessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace LegendScaler;

internal partial class ScaleLegendWindow : ProWindow
{
    private readonly LayoutView _view;
    private readonly LegendTarget _target;
    private bool _ready;
    private bool _busy;

    public ScaleLegendWindow(LayoutView view, LegendTarget target)
    {
        _view = view;
        _target = target;
        InitializeComponent();
        SelectionText.Text = $"{target.Name} | {target.ItemCount} legend items";
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

    private async Task ExecuteAsync(bool createCopy)
    {
        if (_busy || !TryFactor(out var factor)) return;
        _busy = true;
        UpdateInput();
        PercentBox.IsEnabled = false;
        CloseButton.IsEnabled = false;
        try
        {
            var result = await QueuedTask.Run(() => LegendScaleService.Apply(_view, _target, factor, createCopy));
            _busy = false;
            DialogResult = true;
            if (!result.FitsFrame)
                ProMessageBox.Show("The scaled legend does not fit its frame. Inspect the result and enlarge the frame or use layout Undo.", "Legend Scaler");
        }
        catch (Exception ex)
        {
            _busy = false;
            UpdateInput();
            ErrorText.Text = ex.Message;
        }
        finally
        {
            _busy = false;
            PercentBox.IsEnabled = true;
            CloseButton.IsEnabled = true;
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_busy) e.Cancel = true;
        base.OnClosing(e);
    }
}
