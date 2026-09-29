using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ArcGIS.Desktop.Framework.Controls;
using ArcGIS.Desktop.Framework.Threading.Tasks;

namespace MultipleLeaders;

internal sealed record CalloutOptions(string Text, double FontSize, double LineWidth, string? SourceField);

internal partial class CalloutOptionsWindow : ProWindow
{
    private readonly SelectionSnapshot _selection;
    private bool _ready;
    private bool _busy;
    private bool _closed;
    private readonly CancellationTokenSource _cancellation;
    private string? _sourceField;
    public CalloutOptions? Options { get; private set; }

    public CalloutOptionsWindow(SelectionSnapshot selection, CancellationToken cancellationToken = default)
    {
        _selection = selection;
        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        InitializeComponent();
        SelectionText.Text = $"{selection.ObjectIds.Count} selected points in {selection.LayerName}";
        FieldBox.ItemsSource = selection.TextFields;
        if (selection.TextFields.Count > 0) FieldBox.SelectedIndex = 0;
        FontBox.Text = 10d.ToString(CultureInfo.CurrentCulture);
        WidthBox.Text = 0.75d.ToString(CultureInfo.CurrentCulture);
        _ready = true;
        UpdateInput();
        Loaded += (_, _) => LabelBox.Focus();
    }

    private void InputChanged(object sender, TextChangedEventArgs e)
    {
        if (_ready && ReferenceEquals(sender, LabelBox)) _sourceField = null;
        if (_ready) UpdateInput();
    }

    private bool TryOptions(out CalloutOptions? options, out string error)
    {
        options = null;
        error = "";
        if (string.IsNullOrWhiteSpace(LabelBox.Text))
        {
            error = "Enter label text or use a field shared by the selected points.";
            return false;
        }
        if (LabelBox.Text.Trim().Length > 500)
        {
            error = "Label text must contain at most 500 characters.";
            return false;
        }
        if (!TryNumber(FontBox.Text, 6, 72, out var fontSize))
        {
            error = "Font size must be between 6 and 72 points.";
            return false;
        }
        if (!TryNumber(WidthBox.Text, 0.1, 5, out var lineWidth))
        {
            error = "Line width must be between 0.1 and 5 points.";
            return false;
        }
        options = new(LabelBox.Text.Trim(), fontSize, lineWidth, _sourceField);
        return true;
    }

    private static bool TryNumber(string value, double minimum, double maximum, out double number) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out number) &&
        double.IsFinite(number) && number >= minimum && number <= maximum;

    private void UpdateInput()
    {
        if (_closed) return;
        CreateButton.IsEnabled = TryOptions(out _, out var error) && !_busy;
        FieldButton.IsEnabled = !_busy && _selection.TextFields.Count > 0;
        ErrorText.Text = string.IsNullOrWhiteSpace(LabelBox.Text) ? "" : error;
    }

    private async void SharedFieldClick(object sender, RoutedEventArgs e)
    {
        if (_busy || _closed || FieldBox.SelectedItem is not string field) return;
        var token = _cancellation.Token;
        _busy = true;
        UpdateInput();
        LabelBox.IsEnabled = false;
        FieldBox.IsEnabled = false;
        StatusText.Text = "Reading the shared field value. Cancel or Esc closes this window.";
        try
        {
            var text = await QueuedTask.Run(() => CalloutService.GetCommonFieldText(_selection, field, token));
            if (_closed || token.IsCancellationRequested) return;
            LabelBox.Text = text;
            _sourceField = field;
            _busy = false;
            UpdateInput();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (_closed) return;
            _busy = false;
            UpdateInput();
            ErrorText.Text = ex.Message;
        }
        finally
        {
            _busy = false;
            if (!_closed)
            {
                LabelBox.IsEnabled = true;
                FieldBox.IsEnabled = true;
                FieldButton.IsEnabled = _selection.TextFields.Count > 0;
                StatusText.Text = "";
            }
        }
    }

    private void CreateClick(object sender, RoutedEventArgs e)
    {
        if (_busy || !TryOptions(out var options, out var error))
        {
            UpdateInput();
            return;
        }
        Options = options;
        DialogResult = true;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _cancellation.Cancel();
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _closed = true;
        _cancellation.Dispose();
        base.OnClosed(e);
    }

    private void CancelClick(object sender, RoutedEventArgs e) => Close();

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
