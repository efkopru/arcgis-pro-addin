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
    private static double _lastFontSize = 12;
    private static double _lastLineWidth = 0.75;
    public CalloutOptions? Options { get; private set; }

    public CalloutOptionsWindow(SelectionSnapshot selection, CancellationToken cancellationToken = default)
    {
        _selection = selection;
        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        InitializeComponent();
        MaxHeight = Math.Max(360, SystemParameters.WorkArea.Height - 64);
        SelectionText.Text = $"{selection.ObjectIds.Count} selected points in {selection.LayerName}";
        FieldBox.ItemsSource = selection.TextFields;
        if (selection.TextFields.Count > 0) FieldBox.SelectedIndex = 0;
        FieldBox.IsEnabled = selection.TextFields.Count > 0;
        FieldStatusText.Text = selection.TextFields.Count > 0
            ? "All selected features must have the same nonblank value. You can also type your own text."
            : "This layer has no text fields. Type the shared label above.";
        FontBox.Text = _lastFontSize.ToString(CultureInfo.CurrentCulture);
        WidthBox.Text = _lastLineWidth.ToString(CultureInfo.CurrentCulture);
        _ready = true;
        UpdateInput();
        Loaded += (_, _) => LabelBox.Focus();
    }

    private void InputChanged(object sender, TextChangedEventArgs e)
    {
        if (_ready && ReferenceEquals(sender, LabelBox))
        {
            if (_sourceField is not null) FieldStatusText.Text = "Using your edited text instead of the attribute value.";
            _sourceField = null;
        }
        if (_ready) UpdateInput();
    }

    private bool TryOptions(out CalloutOptions? options, out string error)
    {
        options = null;
        if (!CalloutInput.TryRead(LabelBox.Text, FontBox.Text, WidthBox.Text, CultureInfo.CurrentCulture,
                out var fontSize, out var lineWidth, out error))
            return false;
        options = new(LabelBox.Text.Trim(), fontSize, lineWidth, _sourceField);
        return true;
    }

    private void UpdateInput()
    {
        if (_closed) return;
        CreateButton.IsEnabled = TryOptions(out _, out var error) && !_busy;
        FieldButton.IsEnabled = !_busy && _selection.TextFields.Count > 0;
        ErrorText.Text = string.IsNullOrWhiteSpace(LabelBox.Text) ? "" : error;
        CharacterCountText.Text = $"{LabelBox.Text.Trim().Length} / 500 characters";
        PreviewLabel.Text = string.IsNullOrWhiteSpace(LabelBox.Text) ? "Shared label" : LabelBox.Text.Trim();
        if (CalloutInput.TryNumber(FontBox.Text, 6, 72, CultureInfo.CurrentCulture, out var font))
            PreviewLabel.FontSize = font * 96 / 72;
        if (CalloutInput.TryNumber(WidthBox.Text, 0.1, 5, CultureInfo.CurrentCulture, out var width))
            PreviewLeaders.StrokeThickness = width * 96 / 72;
    }

    private void FontPresetClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string value })
            FontBox.Text = double.Parse(value, CultureInfo.InvariantCulture).ToString(CultureInfo.CurrentCulture);
    }

    private void WidthPresetClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string value })
            WidthBox.Text = double.Parse(value, CultureInfo.InvariantCulture).ToString(CultureInfo.CurrentCulture);
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
            FieldStatusText.Text = $"Loaded {field}. This value is checked again when the label is created.";
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
                FieldBox.IsEnabled = _selection.TextFields.Count > 0;
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
        _lastFontSize = options!.FontSize;
        _lastLineWidth = options.LineWidth;
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
