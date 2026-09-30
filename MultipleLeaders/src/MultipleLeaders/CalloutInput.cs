using System.Globalization;

namespace MultipleLeaders;

/// <summary>Validate dialog input before the user starts a map tool.</summary>
internal static class CalloutInput
{
    public static bool TryRead(string text, string fontText, string widthText, CultureInfo culture,
        out double fontSize, out double lineWidth, out string error)
    {
        fontSize = 0;
        lineWidth = 0;
        if (!CalloutGraphicBuilder.TryNormalizeText(text, out _, out error))
            return false;
        if (!TryNumber(fontText, CalloutGraphicBuilder.MinimumFontSize,
                     CalloutGraphicBuilder.MaximumFontSize, culture, out fontSize))
            error = "Enter a font size between 6 and 72 points.";
        else if (!TryNumber(widthText, CalloutGraphicBuilder.MinimumLineWidth,
                     CalloutGraphicBuilder.MaximumLineWidth, culture, out lineWidth))
            error = "Enter a leader width between 0.1 and 5 points.";
        return error.Length == 0;
    }

    // Float deliberately excludes AllowThousands: a foreign decimal separator
    // must not silently change the requested size into a grouped integer.
    public static bool TryNumber(string text, double min, double max, CultureInfo culture, out double value) =>
        double.TryParse(text, NumberStyles.Float, culture, out value) && double.IsFinite(value) && value >= min && value <= max;
}
