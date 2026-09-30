using ArcGIS.Core.CIM;
using ArcGIS.Desktop.Layouts;

namespace LegendScaler;

internal sealed record LegendTarget(Legend Element, string Name, double Width, double Height, int ItemCount,
    string UnitName, IReadOnlyList<string> Warnings);
internal sealed record ScaleResult(string Name, bool FitsFrame, bool IsCopy);

/// <summary>All calls run on Pro's main worker thread through QueuedTask.Run.</summary>
internal static class LegendScaleService
{
    public static LegendTarget Inspect(LayoutView view)
    {
        ValidateView(view);
        var selected = view.GetSelectedElements();
        if (selected.Count == 0)
            throw new InvalidOperationException("No legend is selected. In the layout Contents pane, click a legend, then open Legend Tools > Resize Legend again.");
        if (selected.Count != 1)
            throw new InvalidOperationException($"{selected.Count} elements are selected. In the layout Contents pane, click just one legend, then open Resize Legend again.");
        if (selected[0] is not Legend legend)
            throw new InvalidOperationException("The selected element is not a legend. In the layout Contents pane, select the legend itself, then open Resize Legend again.");

        Validate(legend);
        var definition = (CIMLegend)legend.GetDefinition();
        UseFixedFontSizes(definition);
        return new(legend, legend.Name, legend.GetWidth(), legend.GetHeight(), definition.Items?.Length ?? 0,
            view.Layout.GetPage().Units.ToString().ToLowerInvariant(), LegendScaling.GetWarnings(definition));
    }

    public static ScaleResult Apply(LayoutView view, LegendTarget target, double factor, bool createCopy,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateView(view);
        var layout = view.Layout;
        if (!layout.GetElementsAsFlattenedList().Contains(target.Element))
            throw new InvalidOperationException("The selected legend is no longer in this layout. Close this window and select it again.");

        var original = target.Element;
        Validate(original);
        var before = (CIMLegend)original.GetDefinition();
        var scaled = LegendScaling.Scale(before, factor);
        var width = original.GetWidth();
        var height = original.GetHeight();
        var x = original.GetX();
        var y = original.GetY();
        var lockedAspect = original.GetLockedAspectRatio();
        Legend? result = null;
        var fitsFrame = false;

        // Layout setters participate in Pro's operation stack. Group them so
        // creating or changing a legend is a single native Undo action.
        cancellationToken.ThrowIfCancellationRequested();
        layout.OperationManager.CreateCompositeOperation(() =>
            CancellableLegendEdit.Run(() =>
            {
                result = createCopy
                    ? (Legend)layout.CopyElements(new[] { original }).Single()
                    : original;

                // Apply to the copy's definition to retain its distinct element
                // identity and parent assigned by CopyElements.
                var definition = createCopy
                    ? LegendScaling.Scale((CIMLegend)result.GetDefinition(), factor)
                    : scaled;
                UseFixedFontSizes(definition);
                cancellationToken.ThrowIfCancellationRequested();
                result.SetDefinition(definition);
                cancellationToken.ThrowIfCancellationRequested();
                Resize(result, width * factor, height * factor, x, y, lockedAspect, cancellationToken);
                if (createCopy)
                {
                    result.SetName($"{target.Name} ({factor * 100:0.##}%)");
                    // Same anchor is preserved; place the copy beside its source.
                    // Use bounds because center/right anchors shift as size changes.
                    var oldBounds = original.GetBounds();
                    var copyBounds = result.GetBounds();
                    var gap = width * 0.08;
                    cancellationToken.ThrowIfCancellationRequested();
                    result.SetX(result.GetX() + oldBounds.XMax + gap - copyBounds.XMin);
                }
                cancellationToken.ThrowIfCancellationRequested();
                fitsFrame = result.DoesFitFrame;
                view.SelectElement(result);
            }, () =>
            {
                if (createCopy && result is not null)
                    layout.DeleteElements(new[] { result });
                else if (!createCopy)
                {
                    original.SetDefinition(before);
                    Resize(original, width, height, x, y, lockedAspect);
                }
            }, cancellationToken),
            createCopy ? "Create scaled legend copy" : "Scale legend proportionally");

        return new(result!.Name, fitsFrame, createCopy);
    }

    private static void Resize(Legend legend, double width, double height, double x, double y, bool lockedAspect,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        legend.SetLockedAspectRatio(false);
        legend.SetWidth(width);
        cancellationToken.ThrowIfCancellationRequested();
        legend.SetHeight(height);
        legend.SetX(x);
        legend.SetY(y);
        cancellationToken.ThrowIfCancellationRequested();
        legend.SetLockedAspectRatio(lockedAspect);
    }

    private static void UseFixedFontSizes(CIMLegend definition)
    {
        // Esri documents AdjustColumns as honoring the configured font sizes.
        // ManualColumns, despite its name, can shrink fonts to fit the frame.
        definition.FittingStrategy = LegendFittingStrategy.AdjustColumns;
        definition.AutoFonts = false;
    }

    private static void Validate(Legend legend)
    {
        if (legend.GetParent(false) is GroupElement)
            throw new InvalidOperationException("This prototype supports legends outside groups. Ungroup the legend before scaling it.");
        if (legend.IsLocked)
            throw new InvalidOperationException("Unlock the legend in the Contents pane before scaling it.");
        if (Math.Abs(legend.GetRotation()) > 0.000001)
            throw new InvalidOperationException("This prototype supports unrotated legends. Set the legend rotation to 0 degrees first.");
        if (!double.IsFinite(legend.GetWidth()) || !double.IsFinite(legend.GetHeight()) ||
            legend.GetWidth() <= 0 || legend.GetHeight() <= 0)
            throw new InvalidOperationException("The legend must have a positive width and height.");
    }

    private static void ValidateView(LayoutView view)
    {
        if (view != LayoutView.Active)
            throw new InvalidOperationException("The active layout changed. Return to the intended layout, select its legend, and open Resize Legend again.");
    }
}
