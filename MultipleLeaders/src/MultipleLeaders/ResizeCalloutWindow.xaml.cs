using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using ArcGIS.Desktop.Framework.Controls;

namespace MultipleLeaders;

internal partial class ResizeCalloutWindow : ProWindow
{
    private bool _ready;
    public double SelectedFontSize { get; private set; }

    public ResizeCalloutWindow(double currentFontSize)
    {
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
        var valid = TrySize(out _);
        ResizeButton.IsEnabled = valid;
        ErrorText.Text = valid ? "" : "Enter a font size between 6 and 72 points.";
    }

    private void ResizeClick(object sender, RoutedEventArgs e)
    {
        if (!TrySize(out var size))
        {
            UpdateInput();
            return;
        }
        SelectedFontSize = size;
        DialogResult = true;
    }
}
