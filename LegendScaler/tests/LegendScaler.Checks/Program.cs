using ArcGIS.Core.CIM;
using System.Globalization;
using LegendScaler;

// Synthetic CIM checks require ArcGIS.Core, but do not start Pro or load Framework.
// Passing establishes transformation behavior, not ArcGIS layout rendering behavior.
var checks = new (string Name, Action Run)[]
{
    ("Input is unchanged and the result is deeply independent", CheckClone),
    ("Mixed font sizes preserve their proportions", CheckFonts),
    ("Legend physical dimensions scale together", CheckDimensions),
    ("Current and default item patches and indents scale", CheckItems),
    ("Zero gaps, negative offsets and inherited sizes remain valid", CheckSpecialValues),
    ("Typography percentages and colors remain unchanged", CheckTypography),
    ("Point text spacing scales; line-spacing multipliers do not", CheckTextSpacing),
    ("Graphic frame spacing, stroke widths and dash lengths scale", CheckFrame),
    ("Layer references and legend behaviors remain unchanged", CheckSemantics),
    ("Identity scaling returns equivalent independent content", CheckIdentity),
    ("Repeated and inverse scaling remain numerically stable", CheckComposition),
    ("Invalid factors and null input fail explicitly", CheckValidation),
    ("Factor boundaries and missing optional content are accepted", CheckOptionalContent),
    ("Unsupported content produces warnings without changing source", CheckWarnings),
    ("Cancellation before queued work prevents mutation", CheckCancelledBeforeEdit),
    ("Cancellation during edits restores the original state", CheckCancelledDuringEdit),
    ("Failed edits restore state and failed restoration is reported", CheckEditRestoration),
    ("Common percentage entries produce the intended size", CheckPercentageInput),
    ("Percentage input respects decimal culture and rejects ambiguous entries", CheckPercentageCulture),
    ("Invalid percentages cannot become scaling operations", CheckInvalidPercentages),
    ("Size feedback distinguishes enlargement, reduction and duplication", CheckPercentageFeedback),
};
var failures = 0;
foreach (var (name, run) in checks)
{
    try { run(); Console.WriteLine($"PASS {name}"); }
    catch (Exception error)
    {
        failures++;
        Console.Error.WriteLine($"FAIL {name}: {error.GetType().Name}: {error.Message}");
    }
}
Console.WriteLine($"{checks.Length - failures}/{checks.Length} checks passed.");
return failures == 0 ? 0 : 1;

static void CheckPercentageInput()
{
    foreach (var (text, factor) in new[] { ("50", 0.5), ("75%", 0.75), (" 125 % ", 1.25), ("100", 1.0), ("200%", 2.0), ("10", 0.1), ("1000", 10.0) })
    {
        Assert(LegendScaleInput.TryParse(text, CultureInfo.InvariantCulture, out var input), $"accepted {text}");
        Near(factor, input.Factor, $"factor for {text}");
    }
}

static void CheckPercentageCulture()
{
    var german = CultureInfo.GetCultureInfo("de-DE");
    Assert(LegendScaleInput.TryParse("125,5 %", german, out var input), "localized decimal accepted");
    Near(1.255, input.Factor, "localized fraction retained");
    Assert(!LegendScaleInput.TryParse("125,5", CultureInfo.GetCultureInfo("en-US"), out _), "comma must not be mistaken for a thousands separator");
    Assert(!LegendScaleInput.TryParse("1.000", german, out _), "ambiguous thousands entry rejected");
}

static void CheckInvalidPercentages()
{
    foreach (var text in new string?[] { null, "", " ", "%", "125%%", "NaN", "Infinity", "0", "-50", "9.99", "1000.01", "abc" })
        Assert(!LegendScaleInput.TryParse(text, CultureInfo.InvariantCulture, out _), $"rejected {text ?? "null"}");
}

static void CheckPercentageFeedback()
{
    var larger = new LegendScaleInput(125);
    Assert(larger.DescribeChange(CultureInfo.InvariantCulture).Contains("25% larger"), "125% means 25% larger");
    var smaller = new LegendScaleInput(75);
    Assert(smaller.DescribeChange(CultureInfo.InvariantCulture).Contains("25% smaller"), "75% means 25% smaller");
    var same = new LegendScaleInput(100);
    Assert(!same.ChangesSize && same.DescribeChange(CultureInfo.InvariantCulture).Contains("copy"), "100% guides duplication rather than a no-op original edit");
}

static void CheckCancelledBeforeEdit()
{
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();
    var changed = false;
    var restored = false;
    Throws<OperationCanceledException>(() => CancellableLegendEdit.Run(
        () => changed = true, () => restored = true, cancellation.Token), "pre-canceled edit");
    Assert(!changed && !restored, "neither mutation nor unnecessary restoration may run");
}

static void CheckCancelledDuringEdit()
{
    using var cancellation = new CancellationTokenSource();
    var width = 10;
    CancellableLegendEdit.Run(() => width = 12, () => width = 10, cancellation.Token);
    Equal(12, width, "successful edit remains");
    Throws<OperationCanceledException>(() => CancellableLegendEdit.Run(
        () => { width = 15; cancellation.Cancel(); }, () => width = 12, cancellation.Token), "cancel in edit");
    Equal(12, width, "unfinished edit restored");
}

static void CheckEditRestoration()
{
    var width = 10;
    Throws<ArgumentException>(() => CancellableLegendEdit.Run(
        () => { width = 20; throw new ArgumentException("change failed"); },
        () => width = 10, CancellationToken.None), "failed change");
    Equal(10, width, "failure restores original");
    try
    {
        CancellableLegendEdit.Run(() => throw new ArgumentException("change failed"),
            () => throw new ApplicationException("restore failed"), CancellationToken.None);
        throw new Exception("Expected restoration error");
    }
    catch (InvalidOperationException ex)
    {
        Assert(ex.InnerException is AggregateException aggregate && aggregate.InnerExceptions.Count == 2,
            "both errors preserved");
        Assert(ex.Message.Contains("Undo"), "actionable restoration failure");
    }
}

static void CheckClone()
{
    var original = Fixture();
    var before = original.ToJson();
    var result = LegendScaling.Scale(original, 1.25);
    Equal(before, original.ToJson(), "source after scaling");
    Assert(!ReferenceEquals(original, result), "legend clone");
    Assert(!ReferenceEquals(original.Items, result.Items), "items array clone");
    Assert(!ReferenceEquals(original.Items[0], result.Items[0]), "item clone");
    Assert(!ReferenceEquals(original.TitleSymbol.Symbol, result.TitleSymbol.Symbol), "text clone");
    Text(result.TitleSymbol).Height = 99;
    result.Items[0].Layer = "changed";
    result.GraphicFrame.BorderGapX = 99;
    Equal(before, original.ToJson(), "source after changing clone");
}

static void CheckFonts()
{
    var original = Fixture();
    var result = LegendScaling.Scale(original, 1.25);
    Near(22.5, Text(result.TitleSymbol).Height, "18pt title at 125%");
    Near(11.25, Text(result.Items[0].LabelSymbol).Height, "9pt label at 125%");
    Near(15, Text(result.Items[1].LabelSymbol).Height, "12pt label at 125%");
    foreach (var (before, after) in TextPairs(original, result))
        Near(before.Height * 1.25, after.Height, "all text roles scale");
}

static void CheckDimensions()
{
    var original = Fixture();
    var result = LegendScaling.Scale(original, 1.25);
    foreach (var property in new[]
    {
        "DefaultPatchHeight", "DefaultPatchWidth", "DescriptionWidth", "GroupGap",
        "HeadingGap", "HorizontalItemGap", "PatchGap", "LabelWidth", "MinFontSize",
        "TextGap", "TitleGap", "ItemGap", "ClassGap", "LayerNameGap", "GroupLayerNameGap"
    }) ScaledProperty(original, result, property, 1.25);
}

static void CheckItems()
{
    var original = Fixture();
    var result = LegendScaling.Scale(original, 0.5);
    foreach (var (before, after) in ItemPairs(original, result))
        foreach (var property in new[] { "PatchWidth", "PatchHeight", "ClassIndent", "HeadingIndent", "LayerNameIndent" })
            ScaledProperty(before, after, property, 0.5);
}

static void CheckSpecialValues()
{
    var original = Fixture();
    original.PatchGap = 0;
    original.TitleGap = 0;
    original.Items[0].ClassIndent = 0;
    original.Items[0].PatchWidth = -1;
    Text(original.TitleSymbol).OffsetX = -2;
    original.GraphicFrame.ShadowOffsetY = -3;
    var result = LegendScaling.Scale(original, 2);
    Near(0, result.PatchGap, "zero patch gap");
    Near(0, result.TitleGap, "zero title gap");
    Near(0, result.Items[0].ClassIndent, "zero indent");
    Near(-1, result.Items[0].PatchWidth, "inherited patch dimension sentinel");
    Near(-4, Text(result.TitleSymbol).OffsetX, "negative text offset");
    Near(-6, result.GraphicFrame.ShadowOffsetY, "negative shadow offset");
}

static void CheckTypography()
{
    var original = Fixture();
    var result = LegendScaling.Scale(original, 1.5);
    foreach (var (before, after) in TextPairs(original, result))
    {
        Equal(before.FontFamilyName, after.FontFamilyName, "font family");
        Equal(before.FontStyleName, after.FontStyleName, "font style");
        Near(before.LetterSpacing, after.LetterSpacing, "letter spacing percentage");
        Near(before.LetterWidth, after.LetterWidth, "letter width percentage");
        Near(before.WordSpacing, after.WordSpacing, "word spacing percentage");
        Near(before.Angle, after.Angle, "text angle");
        Equal(before.Symbol.ToJson(), after.Symbol.ToJson(), "text fill");
        Equal(before.Underline, after.Underline, "underline");
    }
}

static void CheckTextSpacing()
{
    var original = Fixture();
    Text(original.Items[0].LabelSymbol).LineGapType = LineGapType.Multiple;
    Text(original.Items[0].LabelSymbol).LineGap = 1.5;
    Text(original.Items[1].LabelSymbol).LineGapType = LineGapType.Exact;
    Text(original.Items[1].LabelSymbol).LineGap = 14;
    var result = LegendScaling.Scale(original, 2);
    foreach (var (before, after) in TextPairs(original, result))
    {
        foreach (var property in new[] { "HaloSize", "IndentAfter", "IndentBefore", "IndentFirstLine", "OffsetX", "OffsetY", "ShadowOffsetX", "ShadowOffsetY" })
            ScaledProperty(before, after, property, 2);
        Equal(before.LineGapType, after.LineGapType, "line gap interpretation");
    }
    Near(1.5, Text(result.Items[0].LabelSymbol).LineGap, "line multiplier");
    Near(28, Text(result.Items[1].LabelSymbol).LineGap, "exact point spacing");
    Near(6, Text(result.TitleSymbol).LineGap, "extra point spacing");
}

static void CheckFrame()
{
    var original = Fixture();
    var result = LegendScaling.Scale(original, 1.5);
    foreach (var property in new[] { "BackgroundGapX", "BackgroundGapY", "BorderGapX", "BorderGapY", "ShadowOffsetX", "ShadowOffsetY" })
        ScaledProperty(original.GraphicFrame, result.GraphicFrame, property, 1.5);
    Near(20, result.GraphicFrame.BorderCornerRounding, "corner rounding percentage");
    var stroke = (CIMSolidStroke)((CIMPolygonSymbol)result.GraphicFrame.BorderSymbol.Symbol).SymbolLayers[0];
    Near(2.25, stroke.Width, "border stroke width");
    var dashes = (CIMGeometricEffectDashes)stroke.Effects[0];
    Near(6, dashes.DashTemplate[0], "dash length");
    Near(3, dashes.DashTemplate[1], "dash space");
}

static void CheckSemantics()
{
    var original = Fixture();
    var result = LegendScaling.Scale(original, 2);
    Equal(original.Name, result.Name, "element name");
    Equal(original.Title, result.Title, "title text");
    Equal(original.MapFrame, result.MapFrame, "map frame reference");
    Equal(original.Columns, result.Columns, "columns");
    Equal(original.FittingStrategy, result.FittingStrategy, "fitting strategy");
    Equal(original.AutoFonts, result.AutoFonts, "font fitting");
    Equal(original.ScaleSymbols, result.ScaleSymbols, "symbol scaling flag");
    Equal(original.AutoAdd, result.AutoAdd, "auto-add");
    Equal(original.AutoReorder, result.AutoReorder, "reorder");
    Equal(original.AutoVisibility, result.AutoVisibility, "visibility behavior");
    Equal(original.Visible, result.Visible, "element visibility");
    Equal(original.Anchor, result.Anchor, "anchor");
    Near(original.Rotation, result.Rotation, "rotation");
    Near(original.MinScale, result.MinScale, "minimum visibility scale");
    Equal(original.ExcludedLayers[0], result.ExcludedLayers[0], "excluded layer");
    foreach (var (before, after) in ItemPairs(original, result))
    {
        Equal(before.Layer, after.Layer, "layer association");
        Equal(before.Name, after.Name, "item name");
        Equal(before.ScaleToPatch, after.ScaleToPatch, "scale-to-patch");
        Equal(before.IsVisible, after.IsVisible, "item visibility");
        Equal(before.UseMapSeriesShape, after.UseMapSeriesShape, "map series behavior");
        Equal(before.ShowCounts, after.ShowCounts, "show counts");
        Equal(before.CountPrefix, after.CountPrefix, "count prefix");
        Equal(before.ManualColumn, after.ManualColumn, "manual column");
    }
}

static void CheckIdentity()
{
    var original = Fixture();
    var result = LegendScaling.Scale(original, 1);
    Equal(original.ToJson(), result.ToJson(), "identity JSON");
    Assert(!ReferenceEquals(original, result), "identity still clones");
}

static void CheckComposition()
{
    var original = Fixture();
    var twice = LegendScaling.Scale(LegendScaling.Scale(original, 1.25), 1.25);
    var once = LegendScaling.Scale(original, 1.5625);
    foreach (var (expected, actual) in TextPairs(once, twice))
        Near(expected.Height, actual.Height, "composed text height");
    Near(once.DefaultPatchWidth, twice.DefaultPatchWidth, "composed patch width");
    Near(once.TitleGap, twice.TitleGap, "composed title gap");
    var restored = original;
    for (var index = 0; index < 20; index++)
        restored = LegendScaling.Scale(LegendScaling.Scale(restored, 1.25), 0.8);
    foreach (var (expected, actual) in TextPairs(original, restored))
        Near(expected.Height, actual.Height, "inverse text height");
    Near(original.DefaultPatchWidth, restored.DefaultPatchWidth, "inverse patch width");
    Near(original.TitleGap, restored.TitleGap, "inverse title gap");
}

static void CheckValidation()
{
    foreach (var factor in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, 0, -1, 0.099, 10.001 })
        Throws<ArgumentOutOfRangeException>(() => LegendScaling.Scale(Fixture(), factor), $"factor {factor}");
    Throws<ArgumentNullException>(() => LegendScaling.Scale(null!, 1), "null legend");
}

static void CheckOptionalContent()
{
    Near(1.8, Text(LegendScaling.Scale(Fixture(), 0.1).TitleSymbol).Height, "minimum factor");
    Near(180, Text(LegendScaling.Scale(Fixture(), 10).TitleSymbol).Height, "maximum factor");
    var original = new CIMLegend { Items = [], TitleSymbol = null, DefaultLegendItem = null, GraphicFrame = null };
    var result = LegendScaling.Scale(original, 1.25);
    Assert(result.Items is null || result.Items.Length == 0, "no items introduced");
    Assert(result.TitleSymbol is null, "no title style introduced");
    original.Items = [new CIMHorizontalLegendItem { LabelSymbol = null }];
    result = LegendScaling.Scale(original, 1.25);
    Assert(result.Items[0].LabelSymbol is null, "no label style introduced");
}

static void CheckWarnings()
{
    var original = Fixture();
    original.Title = "<FNT size='24'>Population</FNT>";
    Text(original.TitleSymbol).OffsetZ = 3;
    var before = original.ToJson();
    var warnings = LegendScaling.GetWarnings(original);
    Equal(before, original.ToJson(), "warning scan does not mutate source");
    Assert(warnings.Any(value => value.Contains("Inline", StringComparison.OrdinalIgnoreCase)), "inline size warning");
    Assert(warnings.Any(value => value.Contains("3D", StringComparison.OrdinalIgnoreCase)), "3D warning");
    Assert(warnings.Any(value => value.Contains("font", StringComparison.OrdinalIgnoreCase)), "font-fitting warning");
    Equal(warnings.Count, warnings.Distinct().Count(), "deduplicated warnings");
    Near(3, Text(LegendScaling.Scale(original, 2).TitleSymbol).OffsetZ, "3D offset preserved");
}

static CIMLegend Fixture() => new()
{
    Name = "Legend for population map", Title = "Population", TitleSymbol = Symbol(18, "Arial", "Bold"),
    MapFrame = "CIMPATH=map/population-map.xml", Visible = true, Rotation = 15,
    DefaultPatchHeight = 10, DefaultPatchWidth = 22, LabelWidth = 48, DescriptionWidth = 72,
    GroupGap = 8, HeadingGap = 4, HorizontalItemGap = 6, PatchGap = 2, MinFontSize = 6,
    TextGap = 5, TitleGap = 9, ItemGap = 7, ClassGap = 3, LayerNameGap = 4, GroupLayerNameGap = 6,
    Columns = 2, FittingStrategy = LegendFittingStrategy.ManualColumns,
    AutoAdd = false, AutoReorder = false, AutoVisibility = true, AutoFonts = false, ScaleSymbols = true,
    MinScale = 250000, ExcludedLayers = ["CIMPATH=map/background.xml"],
    Items = [Item("Population", 9, true), Item("Boundary", 12, false)],
    DefaultLegendItem = Item("Default template", 10, true),
    GraphicFrame = new CIMGraphicFrame
    {
        BackgroundGapX = 2, BackgroundGapY = 3, BorderGapX = 4, BorderGapY = 5,
        ShadowOffsetX = 1, ShadowOffsetY = -1.5, BorderCornerRounding = 20,
        BorderSymbol = new CIMSymbolReference
        {
            Symbol = new CIMPolygonSymbol
            {
                SymbolLayers = [new CIMSolidStroke
                {
                    Width = 1.5, Color = new CIMRGBColor { R = 25, G = 50, B = 75 },
                    Effects = [new CIMGeometricEffectDashes { DashTemplate = [4, 2] }]
                }]
            }
        }
    }
};

static CIMLegendItem Item(string name, double fontSize, bool visible) => new CIMHorizontalLegendItem
{
    Name = name, Layer = $"CIMPATH=map/{name}.xml", IsVisible = visible, ScaleToPatch = visible,
    PatchWidth = 24, PatchHeight = 12, ClassIndent = 2, HeadingIndent = 3, LayerNameIndent = 4,
    LabelSymbol = Symbol(fontSize, "Arial", "Regular"),
    DescriptionSymbol = Symbol(fontSize - 1, "Calibri", "Italic"),
    HeadingSymbol = Symbol(fontSize + 1, "Arial", "Bold"),
    LayerNameSymbol = Symbol(fontSize + 2, "Calibri", "Regular"),
    GroupLayerNameSymbol = Symbol(fontSize + 3, "Arial", "Bold"),
    UseMapSeriesShape = true, ShowCounts = true, CountPrefix = "n=", ManualColumn = 1
};

static CIMSymbolReference Symbol(double height, string family, string style) => new()
{
    Symbol = new CIMTextSymbol
    {
        Height = height, FontFamilyName = family, FontStyleName = style,
        HaloSize = 0.5, IndentAfter = 2, IndentBefore = 3, IndentFirstLine = 1,
        OffsetX = -1, OffsetY = 2, ShadowOffsetX = 0.5, ShadowOffsetY = -0.5,
        LetterSpacing = 10, LetterWidth = 90, WordSpacing = 105, Angle = 10, Underline = true,
        LineGapType = LineGapType.ExtraLeading, LineGap = 3,
        Symbol = new CIMPolygonSymbol
        {
            SymbolLayers = [new CIMSolidFill { Color = new CIMRGBColor { R = 32, G = 64, B = 128 } }]
        }
    }
};

static CIMTextSymbol Text(CIMSymbolReference reference) => (CIMTextSymbol)reference.Symbol;

static IEnumerable<(CIMLegendItem Before, CIMLegendItem After)> ItemPairs(CIMLegend before, CIMLegend after)
{
    yield return (before.DefaultLegendItem, after.DefaultLegendItem);
    for (var index = 0; index < before.Items.Length; index++)
        yield return (before.Items[index], after.Items[index]);
}

static IEnumerable<(CIMTextSymbol Before, CIMTextSymbol After)> TextPairs(CIMLegend before, CIMLegend after)
{
    yield return (Text(before.TitleSymbol), Text(after.TitleSymbol));
    foreach (var (oldItem, newItem) in ItemPairs(before, after))
    {
        yield return (Text(oldItem.LabelSymbol), Text(newItem.LabelSymbol));
        yield return (Text(oldItem.DescriptionSymbol), Text(newItem.DescriptionSymbol));
        yield return (Text(oldItem.HeadingSymbol), Text(newItem.HeadingSymbol));
        yield return (Text(oldItem.LayerNameSymbol), Text(newItem.LayerNameSymbol));
        yield return (Text(oldItem.GroupLayerNameSymbol), Text(newItem.GroupLayerNameSymbol));
    }
}

static void ScaledProperty<T>(T original, T result, string name, double factor)
{
    var property = typeof(T).GetProperty(name) ?? throw new InvalidOperationException($"Missing property {name}");
    Near((double)property.GetValue(original)! * factor, (double)property.GetValue(result)!, name);
}

static void Near(double expected, double actual, string message)
{
    if (!double.IsFinite(actual) || Math.Abs(expected - actual) > 1e-9 * Math.Max(1, Math.Abs(expected)))
        throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
}

static void Equal<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static void Throws<T>(Action action, string message) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new InvalidOperationException($"{message}: expected {typeof(T).Name}");
}
