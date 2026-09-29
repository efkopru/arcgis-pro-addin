using ArcGIS.Core.CIM;
using ArcGIS.Core.Geometry;
using MultipleLeaders;
using System.Text.Json;

// Geometry creation/XY access work without hosting. Native geometry serialization,
// Clone(), spatial-reference operations and rendering require a hosted Pro runtime.
var checks = new (string Name, Action Run)[]
{
    ("One native text graphic has the requested leader endpoints", CheckEndpoints),
    ("Native line callout and text sizes use the requested point sizes", CheckSymbols),
    ("CIM JSON preserves text, style and leader-type metadata", CheckMetadataSerialization),
    ("Input coordinates are unchanged and independent calls do not share symbols", CheckIndependence),
    ("Duplicate coordinates collapse to one leader", CheckDuplicates),
    ("Anchor counts enforce two distinct locations and a maximum of 100", CheckCounts),
    ("Label text is trimmed and treated literally", CheckLiteralText),
    ("Missing and invalid text is rejected", CheckInvalidText),
    ("Nonfinite and out-of-range sizes are rejected", CheckInvalidSizes),
    ("Missing, empty and nonfinite point locations are rejected", CheckInvalidPoints),
    ("Boundary sizes and the maximum anchor count are accepted", CheckBoundaries),
    ("Refresh rejects invalid input before native geometry operations", CheckRefreshValidation),
    ("Cancel before the first click prevents placement", CheckCancelBeforeClick),
    ("Repeated clicks cannot create duplicate labels", CheckSinglePlacement),
    ("Cancellation prevents a delayed queued creation and is isolated to its request", CheckQueuedCancellation),
    ("Tool exit waits for callbacks and coalesces repeated cancellation", CheckDeferredExit),
    ("An intervening callback postpones a posted tool exit without spinning", CheckInterveningCallback),
    ("Stale exit requests cannot deactivate a newer placement", CheckStaleExit),
    ("A pending exit respects a different tool selected by the user", CheckDifferentTool),
    ("Deactivation and reentrant cancellation release the exit busy state", CheckExitReentrancy),
    ("A failed tool transition reports its error and allows a later request", CheckExitFailure),
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
Console.WriteLine("NOT VERIFIED: Pro rendering, geometry persistence, projection, and successful Refresh Leaders.");
return failures == 0 ? 0 : 1;

static void CheckCancelBeforeClick()
{
    var request = new PlacementLifetime();
    request.Cancel();
    request.Cancel();
    Assert(!request.TryClaimPlacement(), "canceled request must reject map clicks");
    Throws<OperationCanceledException>(() => request.Token.ThrowIfCancellationRequested(), "cancellation token");
}

static void CheckSinglePlacement()
{
    var request = new PlacementLifetime();
    var claims = 0;
    Parallel.For(0, 100, _ => { if (request.TryClaimPlacement()) Interlocked.Increment(ref claims); });
    Equal(1, claims, "only one placement can be scheduled");
    request.Cancel();
    Assert(!request.TryClaimPlacement(), "cannot reuse canceled placement");
}

static void CheckQueuedCancellation()
{
    var oldRequest = new PlacementLifetime();
    var created = false;
    Assert(oldRequest.TryClaimPlacement(), "first click accepted");
    Action queuedCreate = () => { oldRequest.Token.ThrowIfCancellationRequested(); created = true; };
    oldRequest.Cancel();
    var newRequest = new PlacementLifetime();
    Throws<OperationCanceledException>(queuedCreate, "delayed worker must cancel");
    Assert(!created, "no label after cancellation");
    Assert(newRequest.TryClaimPlacement() && !newRequest.Token.IsCancellationRequested,
        "old cancellation must not affect a new request");
}

static void CheckDeferredExit()
{
    var queued = new Queue<Action>();
    var switches = 0;
    var insideCallback = true;
    var exits = new PlacementExitScheduler(queued.Enqueue, () => true, () =>
    {
        Assert(!insideCallback, "tool transition cannot run inside a MapTool callback");
        switches++;
        return Task.CompletedTask;
    }, error => throw new InvalidOperationException("unexpected tool exit error", error));
    var revision = exits.BeginRequest();
    var first = exits.EnterCallback();
    var nested = exits.EnterCallback();
    exits.RequestExit(revision);
    exits.RequestExit(revision);
    Assert(exits.IsBusy, "pending exit disables a new placement");
    Equal(0, queued.Count, "no dispatcher work while callback is active");
    nested.Dispose();
    Equal(0, queued.Count, "outer callback still owns its lease");
    first.Dispose();
    first.Dispose();
    insideCallback = false;
    Equal(1, queued.Count, "all cancellation requests coalesce to one post");
    Equal(0, switches, "posting must not switch tools inline");
    queued.Dequeue()();
    Equal(1, switches, "one switch after callback exit");
    Assert(!exits.IsBusy, "completed exit releases busy state");
}

static void CheckInterveningCallback()
{
    var queued = new Queue<Action>();
    var switches = 0;
    var exits = new PlacementExitScheduler(queued.Enqueue, () => true,
        () => { switches++; return Task.CompletedTask; }, _ => { });
    var revision = exits.BeginRequest();
    exits.RequestExit(revision);
    var callback = exits.EnterCallback();
    queued.Dequeue()();
    Equal(0, switches, "new callback blocks the already-posted exit");
    Equal(0, queued.Count, "do not spin/repost while the callback is active");
    callback.Dispose();
    Equal(1, queued.Count, "post once when the intervening callback leaves");
    queued.Dequeue()();
    Equal(1, switches, "exit still runs after postponement");
}

static void CheckStaleExit()
{
    var queued = new Queue<Action>();
    var switches = 0;
    var exits = new PlacementExitScheduler(queued.Enqueue, () => true,
        () => { switches++; return Task.CompletedTask; }, _ => { });
    var oldRevision = exits.BeginRequest();
    exits.RequestExit(oldRevision);
    var currentRevision = exits.BeginRequest();
    queued.Dequeue()();
    Equal(0, switches, "queued old exit cannot stop the newer placement");
    exits.RequestExit(oldRevision);
    Equal(0, queued.Count, "late old callback cannot queue another exit");
    exits.RequestExit(currentRevision);
    queued.Dequeue()();
    Equal(1, switches, "current request can still exit normally");
}

static void CheckDifferentTool()
{
    var queued = new Queue<Action>();
    var toolIsOurs = true;
    var switches = 0;
    var exits = new PlacementExitScheduler(queued.Enqueue, () => toolIsOurs,
        () => { switches++; return Task.CompletedTask; }, _ => { });
    var revision = exits.BeginRequest();
    exits.RequestExit(revision);
    toolIsOurs = false;
    queued.Dequeue()();
    Equal(0, switches, "never replace a tool the user switched to before dispatch");
    Assert(!exits.IsBusy, "abandoned exit releases busy state");
    // Cancellation can run while activation is still pending. Its final continuation
    // can retry with the same revision after that activation completes.
    toolIsOurs = true;
    exits.RequestExit(revision);
    queued.Dequeue()();
    Equal(1, switches, "same-revision cancellation retries after late activation");
}

static void CheckExitReentrancy()
{
    var queued = new Queue<Action>();
    var transition = new TaskCompletionSource();
    var switches = 0;
    PlacementExitScheduler exits = null!;
    exits = new PlacementExitScheduler(queued.Enqueue, () => true, () =>
    {
        switches++;
        using (exits.EnterCallback()) exits.RequestExit(exits.Revision);
        exits.Invalidate(); // Our own tool switch raises OnToolDeactivateAsync.
        return transition.Task;
    }, _ => { });
    var revision = exits.BeginRequest();
    exits.RequestExit(revision);
    queued.Dequeue()();
    Equal(1, switches, "reentrant sketch cancellation cannot start another transition");
    Equal(0, queued.Count, "no recursive transition is posted");
    Assert(exits.IsBusy, "deactivation must not prematurely release an in-flight switch");
    Throws<InvalidOperationException>(() => exits.BeginRequest(), "new request blocked while transition is running");
    transition.SetResult();
    Assert(!exits.IsBusy, "transition completion releases busy state even after revision invalidation");
    Assert(exits.BeginRequest() > revision, "a new request is enabled after transition completion");
}

static void CheckExitFailure()
{
    var queued = new Queue<Action>();
    var errors = new List<Exception>();
    var failed = true;
    var exits = new PlacementExitScheduler(queued.Enqueue, () => true,
        () => failed ? Task.FromException(new InvalidOperationException("test transition failure")) : Task.CompletedTask,
        errors.Add);
    exits.RequestExit(exits.BeginRequest());
    queued.Dequeue()();
    Equal(1, errors.Count, "transition error is observed");
    Assert(!exits.IsBusy, "failed transition releases busy state");
    failed = false;
    exits.RequestExit(exits.BeginRequest());
    queued.Dequeue()();
    Equal(1, errors.Count, "later successful request does not replay the previous error");
    Assert(!exits.IsBusy, "later transition finishes normally");
}

static MapPoint Point(double x, double y) => MapPointBuilderEx.CreateMapPoint(x, y);
static MapPoint Label() => Point(25, 40);
static MapPoint[] Anchors() => [Point(-10, 5), Point(100, 20), Point(40, -25)];
static CIMTextGraphic Graphic() => CalloutGraphicBuilder.Create("Buildings", Label(), Anchors(), 12, 0.75);
static CIMTextSymbol TextSymbol(CIMTextGraphic graphic) => (CIMTextSymbol)graphic.Symbol.Symbol;
static CIMLineCallout Callout(CIMTextGraphic graphic) => (CIMLineCallout)TextSymbol(graphic).Callout;

static void CheckEndpoints()
{
    var anchors = Anchors();
    var graphic = CalloutGraphicBuilder.Create("Buildings", Label(), anchors, 12, 0.75);
    Equal(3, graphic.Leaders.Length, "native leader count");
    SameXY(Label(), (MapPoint)graphic.Shape, "label position");
    Equal(Anchor.CenterPoint, graphic.Placement, "label placement");
    for (var index = 0; index < anchors.Length; index++)
    {
        Assert(graphic.Leaders[index] is CIMLeaderPoint, "leader uses native point geometry");
        SameXY(anchors[index], ((CIMLeaderPoint)graphic.Leaders[index]).Point, $"anchor {index}");
    }
}

static void CheckSymbols()
{
    var graphic = Graphic();
    var text = TextSymbol(graphic);
    Near(12, text.Height, "font size");
    Equal("Arial", text.FontFamilyName, "font family");
    Equal(HorizontalAlignment.Center, text.HorizontalAlignment, "horizontal alignment");
    Equal(VerticalAlignment.Center, text.VerticalAlignment, "vertical alignment");
    Assert(text.Callout is CIMBackgroundCallout, "concrete supported callout");
    var callout = Callout(graphic);
    Equal(LeaderLineStyle.Base, callout.LineStyle, "leader style");
    Near(0, callout.LeaderTolerance, "leader tolerance");
    Near(2, callout.Gap, "gap in points");
    var stroke = (CIMSolidStroke)callout.LeaderLineSymbol.SymbolLayers[0];
    Near(0.75, stroke.Width, "line width");
    Assert(stroke.Enable, "stroke enabled");
    var black = (CIMRGBColor)stroke.Color;
    Near(0, black.R, "red channel");
    Near(0, black.G, "green channel");
    Near(0, black.B, "blue channel");
    Near(100, black.Values[3], "opaque stroke");
}

static void CheckMetadataSerialization()
{
    var graphic = Graphic();
    using var document = JsonDocument.Parse(graphic.ToJson());
    var root = document.RootElement;
    Equal("CIMTextGraphic", root.GetProperty("type").GetString(), "graphic type");
    Equal("Buildings", root.GetProperty("text").GetString(), "text");
    var leaders = root.GetProperty("leaders");
    Equal(3, leaders.GetArrayLength(), "serialized leader count");
    foreach (var leader in leaders.EnumerateArray())
        Equal("CIMLeaderPoint", leader.GetProperty("type").GetString(), "serialized leader type");
    var symbol = root.GetProperty("symbol").GetProperty("symbol");
    Near(12, symbol.GetProperty("height").GetDouble(), "serialized font size");
    Equal("CIMBackgroundCallout", symbol.GetProperty("callout").GetProperty("type").GetString(), "serialized callout");
    // Standalone CIM JSON omits Shape and Leader.Point without Pro host callbacks.
    // Direct geometry checks above intentionally do not claim persistence validation.
}

static void CheckIndependence()
{
    var label = Label();
    var anchors = Anchors();
    var first = CalloutGraphicBuilder.Create("Buildings", label, anchors, 12, 0.75);
    var second = CalloutGraphicBuilder.Create("Buildings", label, anchors, 12, 0.75);
    TextSymbol(first).Height = 30;
    first.Leaders[0] = new CIMLeaderPoint { Point = Point(999, 999) };
    Near(12, TextSymbol(second).Height, "second graphic independent style");
    SameXY(Point(-10, 5), ((CIMLeaderPoint)second.Leaders[0]).Point, "second leader unchanged");
    SameXY(Point(-10, 5), anchors[0], "input anchor unchanged");
    SameXY(Point(25, 40), label, "input label unchanged");
}

static void CheckDuplicates()
{
    var graphic = CalloutGraphicBuilder.Create("Buildings", Label(),
        [Point(10, 10), Point(10, 10), Point(20, 20)], 12, 1);
    Equal(2, graphic.Leaders.Length, "duplicate leader removed");
    SameXY(Point(10, 10), ((CIMLeaderPoint)graphic.Leaders[0]).Point, "first endpoint");
    SameXY(Point(20, 20), ((CIMLeaderPoint)graphic.Leaders[1]).Point, "second endpoint");
}

static void CheckCounts()
{
    Throws<ArgumentException>(() => CalloutGraphicBuilder.Create("x", Label(), [], 12, 1), "empty anchors");
    Throws<ArgumentException>(() => CalloutGraphicBuilder.Create("x", Label(), [Point(0, 0)], 12, 1), "one anchor");
    Throws<ArgumentException>(() => CalloutGraphicBuilder.Create("x", Label(), [Point(0, 0), Point(0, 0)], 12, 1), "one unique endpoint");
    var tooMany = Enumerable.Range(0, 101).Select(index => Point(index, index)).ToArray();
    Throws<ArgumentException>(() => CalloutGraphicBuilder.Create("x", Label(), tooMany, 12, 1), "101 anchors");
}

static void CheckLiteralText()
{
    var graphic = CalloutGraphicBuilder.Create("  A&B <FNT size='30'>C</FNT>  ", Label(), Anchors(), 12, 1);
    Equal("A&amp;B &lt;FNT size='30'&gt;C&lt;/FNT&gt;", graphic.Text, "markup escaped once");
    var multiline = CalloutGraphicBuilder.Create("Line one\nLine two", Label(), Anchors(), 12, 1);
    Equal("Line one\nLine two", multiline.Text, "multiline text retained");
}

static void CheckInvalidText()
{
    Throws<ArgumentNullException>(() => CalloutGraphicBuilder.Create(null!, Label(), Anchors(), 12, 1), "null text");
    foreach (var text in new[] { "", " \t\n ", new string('x', 501), "bad\0text" })
        Throws<ArgumentException>(() => CalloutGraphicBuilder.Create(text, Label(), Anchors(), 12, 1), "invalid text");
}

static void CheckInvalidSizes()
{
    foreach (var size in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, 0, -1, 5.99, 72.01 })
        Throws<ArgumentOutOfRangeException>(() => CalloutGraphicBuilder.Create("x", Label(), Anchors(), size, 1), $"font {size}");
    foreach (var width in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, 0, -1, 0.099, 5.001 })
        Throws<ArgumentOutOfRangeException>(() => CalloutGraphicBuilder.Create("x", Label(), Anchors(), 12, width), $"stroke {width}");
}

static void CheckInvalidPoints()
{
    Throws<ArgumentNullException>(() => CalloutGraphicBuilder.Create("x", null!, Anchors(), 12, 1), "null label");
    Throws<ArgumentNullException>(() => CalloutGraphicBuilder.Create("x", Label(), null!, 12, 1), "null anchors");
    Throws<ArgumentException>(() => CalloutGraphicBuilder.Create("x", Label(), [null!, Point(0, 0)], 12, 1), "null anchor");
    foreach (var invalid in new[] { Point(double.NaN, double.NaN), Point(double.PositiveInfinity, 0), Point(0, double.NegativeInfinity) })
    {
        Throws<ArgumentException>(() => CalloutGraphicBuilder.Create("x", invalid, Anchors(), 12, 1), "invalid label coordinates");
        Throws<ArgumentException>(() => CalloutGraphicBuilder.Create("x", Label(), [invalid, Point(0, 0)], 12, 1), "invalid anchor coordinates");
    }
}

static void CheckBoundaries()
{
    var first = CalloutGraphicBuilder.Create(new string('x', 500), Label(), Anchors(), 6, 0.1);
    Near(6, TextSymbol(first).Height, "minimum font");
    Near(0.1, ((CIMSolidStroke)Callout(first).LeaderLineSymbol.SymbolLayers[0]).Width, "minimum stroke");
    var hundred = Enumerable.Range(0, 100).Select(index => Point(index, index * 2)).ToArray();
    var last = CalloutGraphicBuilder.Create("x", Label(), hundred, 72, 5);
    Equal(100, last.Leaders.Length, "100 leaders");
    Near(72, TextSymbol(last).Height, "maximum font");
    Near(5, ((CIMSolidStroke)Callout(last).LeaderLineSymbol.SymbolLayers[0]).Width, "maximum stroke");
}

static void CheckRefreshValidation()
{
    Throws<ArgumentNullException>(() => CalloutGraphicBuilder.UpdateAnchors(null!, Anchors()), "null graphic");
    Throws<ArgumentNullException>(() => CalloutGraphicBuilder.UpdateAnchors(Graphic(), null!), "null replacement anchors");
    Throws<ArgumentException>(() => CalloutGraphicBuilder.UpdateAnchors(new CIMTextGraphic(), Anchors()), "missing label position");
    Throws<ArgumentException>(() => CalloutGraphicBuilder.UpdateAnchors(Graphic(), [Point(0, 0)]), "invalid replacement count");
}

static void SameXY(MapPoint expected, MapPoint actual, string message)
{
    Near(expected.X, actual.X, message + " X");
    Near(expected.Y, actual.Y, message + " Y");
}

static void Near(double expected, double actual, string message)
{
    if (!double.IsFinite(actual) || Math.Abs(expected - actual) > 1e-9)
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
