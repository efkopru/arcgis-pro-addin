using System;
using System.Collections.Generic;
using System.Linq;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Geometry;

namespace MultipleLeaders;

/// <summary>
/// Builds one native map text graphic with multiple point leaders.
/// Geometry must already be projected into the map's spatial reference.
/// </summary>
public static class CalloutGraphicBuilder
{
    public const int MinimumAnchorCount = 2;
    public const int MaximumAnchorCount = 100;
    public const int MaximumTextLength = 500;
    public const double MinimumFontSize = 6;
    public const double MaximumFontSize = 72;
    public const double MinimumLineWidth = 0.1;
    public const double MaximumLineWidth = 5;

    /// <summary>
    /// Sizes, line width and callout gaps are in points, independent of map units.
    /// The resulting CIM is independent of other calls; immutable input points can be shared.
    /// This creates graphics, not feature-linked annotation or a Maplex label expression.
    /// </summary>
    public static CIMTextGraphic Create(
        string text,
        MapPoint labelPosition,
        IReadOnlyList<MapPoint> anchors,
        double fontSize,
        double lineWidth)
    {
        var trimmedText = NormalizeText(text);
        ArgumentNullException.ThrowIfNull(labelPosition);
        ArgumentNullException.ThrowIfNull(anchors);

        ValidateSize(fontSize, MinimumFontSize, MaximumFontSize, nameof(fontSize));
        ValidateSize(lineWidth, MinimumLineWidth, MaximumLineWidth, nameof(lineWidth));
        ValidatePoint(labelPosition, nameof(labelPosition));

        var leaders = BuildLeaders(labelPosition, anchors);

        var leaderSymbol = new CIMLineSymbol
        {
            SymbolLayers = new CIMSymbolLayer[]
            {
                new CIMSolidStroke
                {
                    Enable = true,
                    Color = Black(),
                    Width = lineWidth,
                    CapStyle = LineCapStyle.Round,
                    JoinStyle = LineJoinStyle.Round
                }
            }
        };

        var textSymbol = new CIMTextSymbol
        {
            FontFamilyName = "Arial",
            FontStyleName = "Regular",
            Height = fontSize,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Symbol = new CIMPolygonSymbol
            {
                SymbolLayers = new CIMSymbolLayer[]
                {
                    new CIMSolidFill { Enable = true, Color = Black() }
                }
            },
            // The concrete background callout supports point leaders and a gap.
            // Its optional background/accent symbols remain null, drawing only leaders.
            Callout = new CIMBackgroundCallout
            {
                LeaderLineSymbol = leaderSymbol,
                LineStyle = LeaderLineStyle.Base,
                Gap = 2,
                LeaderOffset = 0,
                LeaderTolerance = 0
            }
        };

        return new CIMTextGraphic
        {
            // Treat user-entered values as literal text, never as Pro formatting tags.
            Text = EscapeLiteralText(trimmedText),
            Shape = labelPosition,
            Placement = Anchor.CenterPoint,
            Symbol = new CIMSymbolReference { Symbol = textSymbol },
            Leaders = leaders
        };
    }

    /// <summary>Validates and trims literal label text without adding CIM formatting escapes.</summary>
    public static string NormalizeText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!TryNormalizeText(text, out var normalizedText, out var error))
            throw new ArgumentException(error, nameof(text));
        return normalizedText;
    }

    internal static bool TryNormalizeText(string? text, out string normalizedText, out string error)
    {
        normalizedText = text?.Trim() ?? string.Empty;
        error = "";
        if (normalizedText.Length == 0)
            error = "Enter label text or use a common attribute value.";
        else if (normalizedText.Length > MaximumTextLength)
            error = $"Label text must contain at most {MaximumTextLength} characters.";
        else if (text!.Any(character => char.IsControl(character) && character is not '\r' and not '\n' and not '\t'))
            error = "Remove unsupported control characters from the label text.";
        return error.Length == 0;
    }

    /// <summary>
    /// Returns a deep CIM copy with new anchor positions, preserving the user's label
    /// position, already-escaped text, and symbol edits. Input graphics are not changed.
    /// </summary>
    public static CIMTextGraphic UpdateAnchors(CIMTextGraphic graphic, IReadOnlyList<MapPoint> anchors)
    {
        ArgumentNullException.ThrowIfNull(graphic);
        ArgumentNullException.ThrowIfNull(anchors);
        if (graphic.Shape is not MapPoint labelPosition)
            throw new ArgumentException("The label must be a point text graphic.", nameof(graphic));
        ValidatePoint(labelPosition, nameof(graphic));
        var leaders = BuildLeaders(labelPosition, anchors);
        var result = graphic.Clone();
        result.Leaders = leaders;
        return result;
    }

    private static CIMLeader[] BuildLeaders(MapPoint labelPosition, IReadOnlyList<MapPoint> anchors)
    {
        if (anchors.Count < MinimumAnchorCount || anchors.Count > MaximumAnchorCount)
            throw new ArgumentException($"Select between {MinimumAnchorCount} and {MaximumAnchorCount} anchor features.", nameof(anchors));

        var uniqueCoordinates = new HashSet<(double X, double Y)>();
        var leaders = new List<CIMLeader>(anchors.Count);
        foreach (var anchor in anchors)
        {
            ValidatePoint(anchor, nameof(anchors));
            if (!HaveSameSpatialReference(labelPosition, anchor))
                throw new ArgumentException("All anchors and the label position must use the same spatial reference.", nameof(anchors));

            // Duplicate XY anchors would draw the same leader repeatedly in a 2D map.
            if (uniqueCoordinates.Add((anchor.X, anchor.Y)))
                leaders.Add(new CIMLeaderPoint { Point = anchor });
        }

        if (leaders.Count < MinimumAnchorCount)
            throw new ArgumentException("At least two distinct anchor locations are required.", nameof(anchors));
        return leaders.ToArray();
    }

    private static CIMRGBColor Black() => new() { Values = new double[] { 0, 0, 0, 100 } };

    private static string EscapeLiteralText(string text) => text
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal);

    private static void ValidateSize(double value, double minimum, double maximum, string parameterName)
    {
        if (!double.IsFinite(value) || value < minimum || value > maximum)
            throw new ArgumentOutOfRangeException(parameterName, $"The value must be between {minimum} and {maximum} points.");
    }

    private static void ValidatePoint(MapPoint? point, string parameterName)
    {
        if (point is null || point.IsEmpty || !double.IsFinite(point.X) || !double.IsFinite(point.Y))
            throw new ArgumentException("Every anchor and label location must be a nonempty point with finite XY coordinates.", parameterName);
    }

    private static bool HaveSameSpatialReference(MapPoint first, MapPoint second)
    {
        var firstReference = first.SpatialReference;
        var secondReference = second.SpatialReference;
        return firstReference is null
            ? secondReference is null
            : secondReference is not null && firstReference.IsEqual(secondReference);
    }
}
