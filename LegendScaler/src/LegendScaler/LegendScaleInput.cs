using System.Globalization;

namespace LegendScaler;

/// <summary>Percentage input shared by the dialog and standalone checks.</summary>
internal readonly record struct LegendScaleInput(double Percent)
{
    public double Factor => Percent / 100;
    public bool ChangesSize => Math.Abs(Factor - 1) > 1e-12;

    public string DescribeChange(CultureInfo culture) => !ChangesSize
        ? "Same size. Create a copy to duplicate this legend."
        : $"{Math.Abs(Percent - 100).ToString("0.##", culture)}% {(Percent > 100 ? "larger" : "smaller")} than the current legend.";

    public static bool TryParse(string? text, CultureInfo culture, out LegendScaleInput input)
    {
        var value = text?.Trim() ?? string.Empty;
        var percentSymbol = culture.NumberFormat.PercentSymbol;
        if (value.EndsWith("%", StringComparison.Ordinal))
            value = value[..^1].TrimEnd();
        else if (!string.IsNullOrEmpty(percentSymbol) && value.EndsWith(percentSymbol, StringComparison.Ordinal))
            value = value[..^percentSymbol.Length].TrimEnd();

        var valid = double.TryParse(value, NumberStyles.Float, culture, out var percent)
                    && double.IsFinite(percent) && percent >= 10 && percent <= 1000;
        input = new(valid ? percent : 0);
        return valid;
    }
}
