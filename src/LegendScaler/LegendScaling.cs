using System;
using System.Collections.Generic;
using ArcGIS.Core.CIM;

namespace LegendScaler;

/// <summary>
/// Scales explicitly supported, absolute dimensions on a detached legend definition.
/// Layout geometry and map-layer renderers are deliberately outside this transform.
/// </summary>
public static class LegendScaling
{
    public const double MinimumFactor = 0.1;
    public const double MaximumFactor = 10.0;

    /// <summary>
    /// Returns a deep copy. The input, frame geometry, fitting strategy, map reference,
    /// column assignments, text contents, and percentage-based properties are retained.
    /// </summary>
    public static CIMLegend Scale(CIMLegend original, double factor)
    {
        ArgumentNullException.ThrowIfNull(original);
        if (!double.IsFinite(factor) || factor < MinimumFactor || factor > MaximumFactor)
            throw new ArgumentOutOfRangeException(nameof(factor), factor,
                "Scale must be between 10% and 1000%.");

        var copy = original.Clone();
        new Transformer(factor).Transform(copy);
        return copy;
    }

    /// <summary>
    /// Reports rendering limitations without modifying the supplied definition.
    /// A native Pro rendering check is still required for renderer-derived map symbols.
    /// </summary>
    public static IReadOnlyList<string> GetWarnings(CIMLegend original)
    {
        ArgumentNullException.ThrowIfNull(original);
        var transformer = new Transformer(1);
        transformer.Transform(original.Clone());
        return transformer.Warnings;
    }

    private sealed class Transformer(double factor)
    {
        // A symbol reference can be reused by several text roles. Scale each object once.
        private readonly HashSet<object> _visited = new(ReferenceEqualityComparer.Instance);
        private readonly HashSet<string> _warningSet = new(StringComparer.Ordinal);
        private readonly List<string> _warnings = [];

        public IReadOnlyList<string> Warnings => _warnings.AsReadOnly();

        public void Transform(CIMLegend legend)
        {
            legend.DefaultPatchHeight = Dimension(legend.DefaultPatchHeight);
            legend.DefaultPatchWidth = Dimension(legend.DefaultPatchWidth);
            legend.DescriptionWidth = Dimension(legend.DescriptionWidth);
            legend.LabelWidth = Dimension(legend.LabelWidth);
            legend.MinFontSize = Dimension(legend.MinFontSize);
            legend.GroupGap *= factor;
            legend.HeadingGap *= factor;
            legend.HorizontalItemGap *= factor;
            legend.PatchGap *= factor;
            legend.TextGap *= factor;
            legend.TitleGap *= factor;
            legend.ItemGap *= factor;
            legend.ClassGap *= factor;
            legend.LayerNameGap *= factor;
            legend.GroupLayerNameGap *= factor;

            ScaleReference(legend.TitleSymbol);
            ScaleItem(legend.DefaultLegendItem);
            foreach (var item in legend.Items ?? [])
                ScaleItem(item);
            ScaleFrame(legend.GraphicFrame);

            if (legend.AutoFonts || legend.FittingStrategy is LegendFittingStrategy.AdjustSize
                or LegendFittingStrategy.AdjustColumnsAndSize or LegendFittingStrategy.ManualColumns)
                Warn("Automatic font fitting can change the requested text sizes during layout rendering.");
            if (legend.FittingStrategy == LegendFittingStrategy.AdjustFrame)
                Warn("Adjust frame fitting can change the final legend frame dimensions.");
            if (!legend.ScaleSymbols)
                Warn("Scale symbols is disabled on this legend; map symbols may retain their original size.");
            if (ContainsSizeMarkup(legend.Title))
                Warn("Inline text formatting can override the scaled font size; formatting tags are preserved.");
        }

        // Negative dimension values can represent inherited or automatic sizes in CIM.
        private double Dimension(double value) => value < 0 ? value : value * factor;

        private void ScaleItem(CIMLegendItem? item)
        {
            if (item is null || !_visited.Add(item))
                return;
            if (item is CIMHorizontalBarLegendItem or CIMNestedLegendItem)
                Warn("Bar and nested legend items have leader lines and outlines whose dimensions are preserved; these styles need visual verification.");
            item.PatchHeight = Dimension(item.PatchHeight);
            item.PatchWidth = Dimension(item.PatchWidth);
            item.ClassIndent *= factor;
            item.HeadingIndent *= factor;
            item.LayerNameIndent *= factor;
            ScaleReference(item.LayerNameSymbol);
            ScaleReference(item.HeadingSymbol);
            ScaleReference(item.LabelSymbol);
            ScaleReference(item.DescriptionSymbol);
            ScaleReference(item.GroupLayerNameSymbol);

            if (!item.ScaleToPatch)
                Warn("Some legend items do not scale their map symbols to the patch. Patch dimensions will scale, but symbol sizes require visual verification.");
            if (ContainsSizeMarkup(item.Name))
                Warn("Inline text formatting can override the scaled font size; formatting tags are preserved.");
        }

        private void ScaleFrame(CIMGraphicFrame? frame)
        {
            if (frame is null || !_visited.Add(frame))
                return;
            frame.BackgroundGapX *= factor;
            frame.BackgroundGapY *= factor;
            frame.BorderGapX *= factor;
            frame.BorderGapY *= factor;
            frame.ShadowOffsetX *= factor;
            frame.ShadowOffsetY *= factor;
            // Corner rounding is 0..100 percent, not a page-unit distance.
            ScaleReference(frame.BackgroundSymbol);
            ScaleReference(frame.BorderSymbol);
            ScaleReference(frame.ShadowSymbol);
        }

        private void ScaleReference(CIMSymbolReference? reference)
        {
            if (reference is null || !_visited.Add(reference))
                return;
            if (reference.PrimitiveOverrides is { Length: > 0 })
                Warn("Symbol property overrides are preserved and may override scaled dimensions.");
            if (reference.ScaleDependentSizeVariation is { Length: > 0 })
                Warn("Scale-dependent size variations are preserved and may override scaled dimensions.");
            if (reference.Symbol is null && !string.IsNullOrWhiteSpace(reference.StylePath))
                Warn("A style reference has no embedded symbol; its dimensions cannot be scaled here.");
            ScaleSymbol(reference.Symbol);
        }

        private void ScaleSymbol(CIMSymbol? symbol)
        {
            if (symbol is null || !_visited.Add(symbol))
                return;

            if (symbol is CIMTextSymbol text)
            {
                text.Height = Dimension(text.Height);
                text.HaloSize = Dimension(text.HaloSize);
                text.IndentAfter *= factor;
                text.IndentBefore *= factor;
                text.IndentFirstLine *= factor;
                text.OffsetX *= factor;
                text.OffsetY *= factor;
                text.ShadowOffsetX *= factor;
                text.ShadowOffsetY *= factor;
                if (text.LineGapType != LineGapType.Multiple)
                    text.LineGap *= factor;
                // LetterSpacing, LetterWidth, WordSpacing and Multiple leading are ratios.
                ScaleSymbol(text.Symbol);
                ScaleSymbol(text.HaloSymbol);
                if (text.Callout is not null)
                    Warn("Text callout geometry is preserved; callout margins and leaders are not scaled.");
                if (text.Symbol3DProperties is not null || text.Depth3D != 0 || text.OffsetZ != 0)
                    Warn("3D symbol dimensions are preserved; this prototype scales layout dimensions in 2D.");
                return;
            }

            if (symbol is not CIMMultiLayerSymbol multilayer)
            {
                Warn($"Dimensions in {symbol.GetType().Name} are not scaled.");
                return;
            }
            if (multilayer.UseRealWorldSymbolSizes)
            {
                Warn("A symbol uses real-world units. Its dimensions are preserved.");
                return;
            }
            ScaleEffects(multilayer.Effects);
            foreach (var layer in multilayer.SymbolLayers ?? [])
            {
                if (layer is null || !_visited.Add(layer))
                    continue;
                ScaleEffects(layer.Effects);
                switch (layer)
                {
                    case CIMSolidStroke stroke:
                        stroke.Width = Dimension(stroke.Width);
                        break;
                    case CIMStroke stroke:
                        stroke.Width = Dimension(stroke.Width);
                        Warn($"Only the stroke width is scaled in {stroke.GetType().Name}; other pattern dimensions are preserved.");
                        break;
                    case CIMSolidFill:
                        break;
                    case CIMPictureFill picture:
                        picture.Height = Dimension(picture.Height);
                        picture.OffsetX *= factor;
                        picture.OffsetY *= factor;
                        // ScaleX is an aspect-ratio multiplier, so it stays unchanged.
                        break;
                    case CIMMarker marker:
                        marker.Size = Dimension(marker.Size);
                        marker.OffsetX *= factor;
                        marker.OffsetY *= factor;
                        if (marker.MarkerPlacement is not null)
                            Warn("Marker sizes are scaled, but marker-placement distances are preserved.");
                        // Size scales marker geometry; scaling its internals again would double it.
                        break;
                    default:
                        Warn($"Dimensions in {layer.GetType().Name} are preserved; complex fills and procedural symbols need visual verification.");
                        break;
                }
            }
        }

        private void ScaleEffects(CIMGeometricEffect[]? effects)
        {
            foreach (var effect in effects ?? [])
            {
                if (effect is null || !_visited.Add(effect))
                    continue;
                switch (effect)
                {
                    case CIMGeometricEffectDashes dashes:
                        dashes.CustomEndingOffset *= factor;
                        dashes.OffsetAlongLine *= factor;
                        if (dashes.DashTemplate is { } template)
                            for (var i = 0; i < template.Length; i++)
                                template[i] *= factor;
                        break;
                    case CIMGeometricEffectOffset offset:
                        offset.Offset *= factor;
                        break;
                    case CIMGeometricEffectMove move:
                        move.OffsetX *= factor;
                        move.OffsetY *= factor;
                        break;
                    case CIMGeometricEffectBuffer buffer:
                        buffer.Size *= factor;
                        break;
                    default:
                        Warn($"Dimensions in {effect.GetType().Name} are preserved.");
                        break;
                }
            }
        }

        private void Warn(string message)
        {
            if (_warningSet.Add(message))
                _warnings.Add(message);
        }

        private static bool ContainsSizeMarkup(string? value) =>
            value?.Contains("<FNT", StringComparison.OrdinalIgnoreCase) == true ||
            value?.Contains("<CHR", StringComparison.OrdinalIgnoreCase) == true;
    }
}
