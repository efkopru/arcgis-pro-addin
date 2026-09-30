using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using ArcGIS.Desktop.Framework.Controls;

namespace MultipleLeaders;

internal partial class ResizeCalloutWindow : ProWindow
{
    private bool _ready;
    private readonly double _originalSize;
    public double SelectedFontSize { get; private set; }

    public ResizeCalloutWindow(double currentFontSize)
    {
        _originalSize = currentFontSize;
        InitializeComponent();
        FontBox.Text = currentFontSize.ToString(CultureInfo.CurrentCulture);
        _ready = true;
        UpdateInput();
        Loaded += (_, _) => { FontBox.Focus(); FontBox.SelectAll(); };
    }

    private bool TrySize(out double size) =>
        double.TryParse(FontBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out size) &&
        double.IsFinite(size) && size >= CalloutGraphicBuilder.MinimumFontSize &&
        size <= CalloutGraphicBuilder.MaximumFontSize;

    private void InputChanged(object sender, TextChangedEventArgs e)
    {
        if (_ready) UpdateInput();
    }

    private void UpdateInput()
    {
        var valid = TrySize(out var size);
        ResizeButton.IsEnabled = valid && Math.Abs(size - _originalSize) > 1e-9;
        ErrorText.Text = valid ? "" : "Enter a font size between 6 and 72 points.";
        ChangeText.Text = !valid ? "" : Math.Abs(size - _originalSize) <= 1e-9
            ? $"Current size: {_originalSize:0.##} pt. Choose a different size to apply a change."
            : $"{_originalSize:0.##} pt to {size:0.##} pt. Use Ctrl+Z in the map to undo.";
        if (valid) PreviewText.FontSize = size * 96 / 72;
        PreviewText.ToolTip = "Style sample only; final appearance depends on map scale.";
    }

    private void PresetClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string value })
            FontBox.Text = double.Parse(value, CultureInfo.InvariantCulture).ToString(CultureInfo.CurrentCulture);
    }

    private void ResizeClick(object sender, RoutedEventArgs e)
    {
        if (!TrySize(out var size) || Math.Abs(size - _originalSize) <= 1e-9)
        {
            UpdateInput();
            return;
        }
        SelectedFontSize = size;
        DialogResult = true;
    }
}
