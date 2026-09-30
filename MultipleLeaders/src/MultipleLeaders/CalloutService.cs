using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;

namespace MultipleLeaders;

internal sealed record SelectionSnapshot(
    Map Map,
    FeatureLayer Layer,
    string LayerUri,
    string LayerName,
    IReadOnlyList<long> ObjectIds,
    IReadOnlyList<MapPoint> Anchors,
    IReadOnlyList<string> TextFields)
{
    public required string SourceIdentity { get; init; }
}

internal sealed record EditableCalloutSnapshot(
    Map Map, GraphicsLayer Layer, GraphicElement Element, string Name, double FontSize)
{
    public required string SourceLayerUri { get; init; }
    public required string SourceIdentity { get; init; }
    public required IReadOnlyList<long> ObjectIds { get; init; }
}

internal sealed record CalloutListItem(EditableCalloutSnapshot? Snapshot, string Text,
    string Source, int PointCount, string GraphicsLayer, string Status, bool IsSelected, bool SourceAvailable)
{
    public bool CanEdit => Snapshot is not null;
    public bool CanReconnect => CanEdit && SourceAvailable;
}

/// <summary>All methods run on Pro's main worker thread through QueuedTask.Run.</summary>
internal static class CalloutService
{
    private const string LayerName = "Multiple Leaders";
    private const string Prefix = "MultipleLeaders.";
    private const string SchemaVersion = "1";
    private const int MaximumFeatures = 100;

    public static SelectionSnapshot Capture(MapView view, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var map = ValidateView(view);
        var selected = map.GetSelection().ToDictionary().Where(pair => pair.Value.Count > 0).ToArray();
        cancellationToken.ThrowIfCancellationRequested();
        if (selected.Length != 1 || selected[0].Key is not FeatureLayer layer)
            throw new InvalidOperationException("Select point features from exactly one feature layer. Clear selections in other layers and tables.");

        var ids = selected[0].Value.Distinct().Order().ToArray();
        ValidateIds(ids);
        using var featureClass = OpenPointClass(layer);
        using var definition = featureClass.GetDefinition();
        var fields = definition.GetFields()
            .Where(field => field.FieldType == FieldType.String)
            .Select(field => field.Name)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var anchors = ReadAnchors(featureClass, ids, map.SpatialReference, cancellationToken);
        if (anchors.Select(point => (point.X, point.Y)).Distinct().Take(2).Count() < 2)
            throw new InvalidOperationException("The selected features occupy one point location. Select points at two or more distinct locations before creating a shared label.");
        EnsureTargetVisible(FindTargetLayer(map));
        var snapshot = new SelectionSnapshot(map, layer, layer.URI, layer.Name, ids, anchors, fields)
        {
            SourceIdentity = GetSourceIdentity(layer)
        };
        cancellationToken.ThrowIfCancellationRequested();
        return snapshot;
    }

    public static string GetCommonFieldText(SelectionSnapshot snapshot, string fieldName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var layer = FindSource(snapshot.Map, snapshot.LayerUri);
        using var featureClass = OpenPointClass(layer);
        ValidateSource(layer, snapshot.SourceIdentity);
        return ReadCommonText(featureClass, snapshot.ObjectIds, fieldName, cancellationToken);
    }

    public static string Create(MapView view, SelectionSnapshot snapshot, MapPoint labelPosition,
        string text, double fontSize, double lineWidth, string? sourceField = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var map = ValidateView(view);
        if (map.URI != snapshot.Map.URI)
            throw new InvalidOperationException("The active map changed. Start Create Shared Label again in the source map.");

        ValidateIds(snapshot.ObjectIds);
        var source = FindSource(map, snapshot.LayerUri);
        using var featureClass = OpenPointClass(source);
        ValidateSource(source, snapshot.SourceIdentity);
        // Re-read the captured IDs, never the current selection, after the options dialog.
        var anchors = ReadAnchors(featureClass, snapshot.ObjectIds, map.SpatialReference, cancellationToken);
        if (!string.IsNullOrEmpty(sourceField))
            text = ReadCommonText(featureClass, snapshot.ObjectIds, sourceField, cancellationToken);
        var position = ProjectPoint(labelPosition, map.SpatialReference, cancellationToken);
        var graphic = CalloutGraphicBuilder.Create(text, position, anchors, fontSize, lineWidth);
        var name = $"Shared label ({anchors.Count} points) {Guid.NewGuid():N}";
        var properties = new[]
        {
            Property("Version", SchemaVersion),
            Property("SourceLayerUri", source.URI),
            Property("SourceLayerName", source.Name),
            Property("SourceIdentity", snapshot.SourceIdentity),
            Property("ObjectIds", JsonSerializer.Serialize(snapshot.ObjectIds)),
            Property("TextSource", string.IsNullOrEmpty(sourceField) ? "Manual" : "SharedField"),
            Property("TextField", sourceField ?? string.Empty),
            Property("CreatedUtc", DateTimeOffset.UtcNow.ToString("O"))
        };

        // Graphics created in map coordinates need a layer in the current map CRS.
        // A previously created layer can retain its old CRS after the map changes.
        GraphicsLayer? target = FindTargetLayer(map);
        EnsureTargetVisible(target);
        GraphicElement? element = null;
        var addedLayer = false;
        cancellationToken.ThrowIfCancellationRequested();
        map.OperationManager.CreateCompositeOperation(() =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (target is null)
                {
                    target = LayerFactory.Instance.CreateLayer<GraphicsLayer>(
                        new GraphicsLayerCreationParams { Name = LayerName, IsVisible = true }, map);
                    addedLayer = true;
                }
                EnsureTargetVisible(target);

                cancellationToken.ThrowIfCancellationRequested();
                element = ElementFactory.Instance.CreateGraphicElement(target, graphic, name, true);
                cancellationToken.ThrowIfCancellationRequested();
                element.SetCustomProperties(properties);
                // Cancellation during either SDK mutation must pass through cleanup.
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (Exception createError)
            {
                try
                {
                    if (addedLayer && target is not null)
                        map.RemoveLayer(target);
                    else if (element is not null && target is not null)
                        ((IElementContainer)target).RemoveElement(element.Name);
                }
                catch (Exception cleanupError)
                {
                    throw new InvalidOperationException(
                        $"Creating the label failed: {createError.Message}\nCleanup also failed: {cleanupError.Message}\nUse Undo and inspect the graphics layer before saving.", createError);
                }
                throw;
            }
        }, "Create shared label with multiple leaders");
        // The operation is committed. Do not report cancellation after this boundary.
        return element!.Name;
    }

    private static GraphicsLayer? FindTargetLayer(Map map) =>
        map.GetLayersAsFlattenedList().OfType<GraphicsLayer>().FirstOrDefault(layer =>
            layer.Name == LayerName && layer.GetSpatialReference() is { IsUnknown: false } reference &&
            reference.IsEqual(map.SpatialReference) &&
            (layer.GetElements().Count == 0 || layer.GetElementsAsFlattenedList()
                .Any(element => element.GetCustomProperty(Prefix + "Version") == SchemaVersion)));

    private static void EnsureTargetVisible(GraphicsLayer? layer)
    {
        if (layer is { IsVisible: false })
            throw new InvalidOperationException("Make the Multiple Leaders graphics layer visible in Contents before creating another label.");
    }

    public static string RefreshSelected(MapView view, CancellationToken cancellationToken = default)
    {
        var snapshot = CaptureSelected(view, cancellationToken);
        return Refresh(view, snapshot, cancellationToken);
    }

    public static string Refresh(MapView view, EditableCalloutSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ValidateSnapshot(view, snapshot, cancellationToken);
        var before = GetEditableGraphic(snapshot.Element);
        var position = ResolveGraphicPosition(snapshot, before);
        var anchors = ReadCurrentAnchors(snapshot, position.SpatialReference, cancellationToken);
        var positioned = before.Clone();
        positioned.Shape = position;
        var updated = CalloutGraphicBuilder.UpdateAnchors(positioned, anchors);
        return ApplyGraphic(snapshot, before, updated, "Reconnect shared label leaders", cancellationToken);
    }

    public static EditableCalloutSnapshot CaptureSelected(MapView view,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var map = ValidateView(view);
        var selected = map.GetLayersAsFlattenedList().OfType<GraphicsLayer>()
            .SelectMany(layer => layer.GetSelectedElements().Select(element => (Layer: layer, Element: element)))
            .ToArray();
        cancellationToken.ThrowIfCancellationRequested();
        if (selected.Length == 0)
            throw new InvalidOperationException("No label graphic is selected. Use Graphics > Select and click the shared label, then run this command.");
        if (selected.Length > 1)
            throw new InvalidOperationException($"{selected.Length} graphics are selected. Use Graphics > Select to select one shared label, then run this command.");
        if (selected[0].Element is not GraphicElement element ||
            element.GetCustomProperty(Prefix + "Version") != SchemaVersion)
            throw new InvalidOperationException("The selected graphic is not a linked Multiple Leaders label. Use Graphics > Select to select a label created by this add-in.");
        return CaptureElement(map, selected[0].Layer, element, cancellationToken);
    }

    // Selection ambiguity opens the label list. Invalid map/view state still raises
    // its normal error rather than hiding that error behind an empty picker.
    public static EditableCalloutSnapshot? TryCaptureSelected(MapView view,
        CancellationToken cancellationToken = default)
    {
        var map = ValidateView(view);
        cancellationToken.ThrowIfCancellationRequested();
        var selected = map.GetLayersAsFlattenedList().OfType<GraphicsLayer>()
            .SelectMany(layer => layer.GetSelectedElements().Select(element => (Layer: layer, Element: element)))
            .ToArray();
        if (selected.Length != 1 || selected[0].Element is not GraphicElement element ||
            element.GetCustomProperty(Prefix + "Version") != SchemaVersion) return null;
        try { return CaptureElement(map, selected[0].Layer, element, cancellationToken); }
        catch (InvalidOperationException) { return null; }
    }

    public static IReadOnlyList<CalloutListItem> ListLabels(MapView view,
        CancellationToken cancellationToken = default)
    {
        var map = ValidateView(view);
        var result = new List<CalloutListItem>();
        var sources = map.GetLayersAsFlattenedList().OfType<FeatureLayer>().ToArray();
        foreach (var layer in map.GetLayersAsFlattenedList().OfType<GraphicsLayer>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var selected = layer.GetSelectedElements();
            foreach (var element in layer.GetElementsAsFlattenedList().OfType<GraphicElement>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var version = element.GetCustomProperty(Prefix + "Version");
                if (string.IsNullOrEmpty(version)) continue;
                var text = element.GetGraphic() is CIMTextGraphic graphic
                    ? System.Net.WebUtility.HtmlDecode(graphic.Text ?? string.Empty)
                        .Replace('\r', ' ').Replace('\n', ' ').Trim()
                    : "Unsupported graphic";
                if (string.IsNullOrWhiteSpace(text)) text = "(Empty label)";
                var uri = element.GetCustomProperty(Prefix + "SourceLayerUri");
                var source = sources.FirstOrDefault(candidate => candidate.URI == uri);
                var sourceName = source?.Name ?? element.GetCustomProperty(Prefix + "SourceLayerName") ?? "Unknown source";
                EditableCalloutSnapshot? snapshot = null;
                var status = source is null ? "Source layer missing; resizing remains available." : "Ready";
                try
                {
                    if (version != SchemaVersion)
                        throw new InvalidOperationException("This label uses an unsupported metadata version.");
                    snapshot = CaptureElement(map, layer, element, cancellationToken);
                    if (!layer.IsVisible) status += " Graphics layer is hidden; turn it on in Contents to view edits.";
                }
                catch (InvalidOperationException error) { status = error.Message; }
                result.Add(new(snapshot, text, sourceName, snapshot?.ObjectIds.Count ?? 0,
                    layer.Name, status, selected.Contains(element), source is not null));
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        return result.OrderBy(item => item.Text, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Source, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    private static EditableCalloutSnapshot CaptureElement(Map map, GraphicsLayer layer,
        GraphicElement element, CancellationToken cancellationToken)
    {
        ValidateEditableElement(element);
        var graphic = GetEditableGraphic(element);
        var textSymbol = GetTextSymbol(graphic);
        var sourceUri = element.GetCustomProperty(Prefix + "SourceLayerUri");
        var sourceIdentity = element.GetCustomProperty(Prefix + "SourceIdentity");
        var ids = ReadSourceIds(element);
        if (string.IsNullOrWhiteSpace(sourceUri) || string.IsNullOrWhiteSpace(sourceIdentity))
            throw new InvalidOperationException("This label is missing its source-link metadata. Create a new shared label from the intended source points.");
        cancellationToken.ThrowIfCancellationRequested();
        return new(map, layer, element, element.Name, textSymbol.Height)
        {
            SourceLayerUri = sourceUri,
            SourceIdentity = sourceIdentity,
            ObjectIds = ids
        };
    }

    public static void ValidateMove(MapView view, EditableCalloutSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ValidateSnapshot(view, snapshot, cancellationToken);
        var reference = snapshot.Layer.GetSpatialReference();
        if (reference is null || reference.IsUnknown || !reference.IsEqual(snapshot.Map.SpatialReference))
            throw new InvalidOperationException("Move Label currently requires the map and graphics layer to use the same coordinate system. Restore the map coordinate system used when this graphics layer was created, then try again.");
        // Fail before activating the click tool when the saved source link is already
        // broken. Move rechecks it again at commit time after the user's map click.
        _ = ReadCurrentAnchors(snapshot, reference, cancellationToken);
    }

    public static string Resize(MapView view, EditableCalloutSnapshot snapshot, double fontSize,
        CancellationToken cancellationToken = default)
    {
        ValidateSnapshot(view, snapshot, cancellationToken);
        if (!double.IsFinite(fontSize) || fontSize < 6 || fontSize > 72)
            throw new ArgumentOutOfRangeException(nameof(fontSize), "Font size must be between 6 and 72 points.");
        var before = GetEditableGraphic(snapshot.Element);
        var updated = before.Clone();
        GetTextSymbol(updated).Height = fontSize;
        return ApplyGraphic(snapshot, before, updated, "Resize shared label text", cancellationToken);
    }

    public static string Move(MapView view, EditableCalloutSnapshot snapshot, MapPoint labelPosition,
        CancellationToken cancellationToken = default)
    {
        ValidateSnapshot(view, snapshot, cancellationToken);
        var before = GetEditableGraphic(snapshot.Element);
        var reference = snapshot.Layer.GetSpatialReference();
        // The element placement API takes bare XY rather than a spatial reference.
        // Keep map and layer units identical until differing-CRS placement is verified.
        if (reference is null || reference.IsUnknown || !reference.IsEqual(snapshot.Map.SpatialReference))
            throw new InvalidOperationException("Move Label currently requires the map and graphics layer to use the same coordinate system. Restore the map coordinate system used when this graphics layer was created, then try again.");
        var currentPosition = ProjectPoint(ResolveGraphicPosition(snapshot, before), reference, cancellationToken);
        var position = ProjectPoint(labelPosition, reference, cancellationToken);
        var anchors = ReadCurrentAnchors(snapshot, position.SpatialReference, cancellationToken);
        var originalAnchor = snapshot.Element.GetAnchorPoint();
        var targetAnchor = new Coordinate2D(
            originalAnchor.X + position.X - currentPosition.X,
            originalAnchor.Y + position.Y - currentPosition.Y);
        if (!double.IsFinite(targetAnchor.X) || !double.IsFinite(targetAnchor.Y))
            throw new InvalidOperationException("The label's anchor position could not be resolved.");
        var tolerance = double.IsFinite(reference.XYTolerance) && reference.XYTolerance > 0
            ? Math.Max(reference.XYTolerance * 2, 1e-6)
            : 1e-6;
        cancellationToken.ThrowIfCancellationRequested();
        snapshot.Map.OperationManager.CreateCompositeOperation(() =>
        {
            var mutationAttempted = false;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                mutationAttempted = true;
                // SetGraphic preserves a point-text element's native placement in Pro.
                // Move the actual element before updating its leader endpoints.
                snapshot.Element.SetAnchorPoint(targetAnchor);
                cancellationToken.ThrowIfCancellationRequested();
                var moved = GetEditableGraphic(snapshot.Element);
                var movedPosition = ProjectPoint(ResolveGraphicPosition(snapshot, moved), reference, cancellationToken);
                AssertMovedPosition(movedPosition, position, tolerance);
                var positioned = moved.Clone();
                positioned.Shape = movedPosition;
                var updated = CalloutGraphicBuilder.UpdateAnchors(positioned, anchors);
                snapshot.Element.SetGraphic(updated);
                cancellationToken.ThrowIfCancellationRequested();
                var finalPosition = ProjectPoint(
                    ResolveGraphicPosition(snapshot, GetEditableGraphic(snapshot.Element)), reference, cancellationToken);
                AssertMovedPosition(finalPosition, position, tolerance);
            }
            catch (Exception changeError)
            {
                try
                {
                    if (mutationAttempted)
                    {
                        snapshot.Element.SetAnchorPoint(originalAnchor);
                        snapshot.Element.SetGraphic(before);
                        // Rollback must finish even after cancellation, and must not
                        // silently accept a native placement that failed to restore.
                        var restoredPosition = ProjectPoint(
                            ResolveGraphicPosition(snapshot, GetEditableGraphic(snapshot.Element)),
                            reference, CancellationToken.None);
                        AssertMovedPosition(restoredPosition, currentPosition, tolerance);
                    }
                }
                catch (Exception restoreError)
                {
                    throw new InvalidOperationException(
                        $"Moving the label failed: {changeError.Message}\nRestoration also failed: {restoreError.Message}\nUse Undo and inspect the graphic before saving.", changeError);
                }
                throw;
            }
        }, "Move shared label and reconnect leaders");
        return snapshot.Element.Name;
    }

    private static void AssertMovedPosition(MapPoint actual, MapPoint expected, double tolerance)
    {
        if (Math.Abs(actual.X - expected.X) > tolerance || Math.Abs(actual.Y - expected.Y) > tolerance)
            throw new InvalidOperationException("ArcGIS Pro did not apply the requested label position.");
    }

    private static void ValidateSnapshot(MapView view, EditableCalloutSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var map = ValidateView(view);
        if (map.URI != snapshot.Map.URI)
            throw new InvalidOperationException("The active map changed. Select the shared label in its source map again.");
        if (!map.GetLayersAsFlattenedList().Contains(snapshot.Layer) ||
            !snapshot.Layer.GetElementsAsFlattenedList().Contains(snapshot.Element))
            throw new InvalidOperationException("The selected label was removed or replaced. Use Graphics > Select to select it again.");
        ValidateEditableElement(snapshot.Element);
        if (snapshot.Element.GetCustomProperty(Prefix + "Version") != SchemaVersion ||
            snapshot.Element.GetCustomProperty(Prefix + "SourceLayerUri") != snapshot.SourceLayerUri ||
            snapshot.Element.GetCustomProperty(Prefix + "SourceIdentity") != snapshot.SourceIdentity ||
            !ReadSourceIds(snapshot.Element).SequenceEqual(snapshot.ObjectIds))
            throw new InvalidOperationException("The selected label's source link changed. Select the label again before editing it.");
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidateEditableElement(GraphicElement element)
    {
        if (element.IsLocked)
            throw new InvalidOperationException("Unlock the shared-label graphic before editing it.");
        if (element.GetParent(false) is GroupElement)
            throw new InvalidOperationException("Ungroup the shared-label graphic before editing it.");
    }

    private static long[] ReadSourceIds(GraphicElement element)
    {
        var serialized = element.GetCustomProperty(Prefix + "ObjectIds");
        if (string.IsNullOrWhiteSpace(serialized))
            throw new InvalidOperationException("This label is missing its source object IDs. Create a new label from the intended source points.");
        long[] ids;
        try
        {
            ids = JsonSerializer.Deserialize<long[]>(serialized)
                ?? throw new InvalidOperationException("Missing source object IDs.");
        }
        catch (JsonException error)
        {
            throw new InvalidOperationException("The graphic's saved source object IDs are invalid.", error);
        }
        ValidateIds(ids);
        return ids;
    }

    private static CIMTextGraphic GetEditableGraphic(GraphicElement element)
    {
        if (element.GetGraphic() is not CIMTextGraphic graphic || graphic.Shape is not MapPoint)
            throw new InvalidOperationException("The selected label is no longer a supported point text graphic.");
        return graphic;
    }

    private static CIMTextSymbol GetTextSymbol(CIMTextGraphic graphic) =>
        graphic.Symbol?.Symbol as CIMTextSymbol
        ?? throw new InvalidOperationException("The selected label's text symbol could not be resolved. Select the label again after reopening the map.");

    private static MapPoint ResolveGraphicPosition(EditableCalloutSnapshot snapshot, CIMTextGraphic graphic)
    {
        if (graphic.Shape is MapPoint point && !point.IsEmpty &&
            point.SpatialReference is { IsUnknown: false })
            return point;

        // GetGeometry returns SDK-decoded coordinates. Persisted CIM storage can omit
        // each point's SR and keep it at the graphics-layer storage level instead.
        // Never interpret raw APRX/CIM storage integer coordinates here.
        if (snapshot.Element.GetGeometry() is not MapPoint decoded || decoded.IsEmpty ||
            !double.IsFinite(decoded.X) || !double.IsFinite(decoded.Y))
            throw new InvalidOperationException("The selected label's position could not be resolved as a point.");
        if (decoded.SpatialReference is { IsUnknown: false })
            return decoded;
        var reference = snapshot.Layer.GetSpatialReference();
        if (reference is null || reference.IsUnknown)
            throw new InvalidOperationException("The selected label's graphics layer has no known coordinate system. Its leaders were not changed.");
        return MapPointBuilderEx.CreateMapPoint(decoded.X, decoded.Y, reference);
    }

    private static IReadOnlyList<MapPoint> ReadCurrentAnchors(EditableCalloutSnapshot snapshot,
        SpatialReference reference, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var source = FindSource(snapshot.Map, snapshot.SourceLayerUri);
        using var featureClass = OpenPointClass(source);
        ValidateSource(source, snapshot.SourceIdentity);
        return ReadAnchors(featureClass, snapshot.ObjectIds, reference, cancellationToken);
    }

    private static string ApplyGraphic(EditableCalloutSnapshot snapshot, CIMTextGraphic before,
        CIMTextGraphic updated, string operationName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        snapshot.Map.OperationManager.CreateCompositeOperation(() =>
        {
            var mutationAttempted = false;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                mutationAttempted = true;
                snapshot.Element.SetGraphic(updated);
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (Exception changeError)
            {
                try
                {
                    if (mutationAttempted)
                        snapshot.Element.SetGraphic(before);
                }
                catch (Exception restoreError)
                {
                    throw new InvalidOperationException(
                        $"Editing the label failed: {changeError.Message}\nRestoration also failed: {restoreError.Message}\nUse Undo and inspect the graphic before saving.", changeError);
                }
                throw;
            }
        }, operationName);
        return snapshot.Element.Name;
    }

    private static Map ValidateView(MapView view)
    {
        if (view != MapView.Active || view.Map is null || view.Map.MapType != MapType.Map)
            throw new InvalidOperationException("Activate a 2D map view before using Multiple Leaders.");
        if (view.Map.SpatialReference is null || view.Map.SpatialReference.IsUnknown)
            throw new InvalidOperationException("The map needs a known coordinate system.");
        return view.Map;
    }

    private static FeatureLayer FindSource(Map map, string uri) =>
        map.GetLayersAsFlattenedList().OfType<FeatureLayer>().FirstOrDefault(layer => layer.URI == uri)
        ?? throw new InvalidOperationException("The original source layer is no longer in this map. Create a new label from the replacement layer.");

    private static FeatureClass OpenPointClass(FeatureLayer layer)
    {
        if (layer.ShapeType != esriGeometryType.esriGeometryPoint)
            throw new InvalidOperationException("This version supports point feature layers. Multipoint, line, and polygon layers are not supported.");
        return layer.GetFeatureClass()
            ?? throw new InvalidOperationException("The source feature class is unavailable. Repair its data source first.");
    }

    private static void ValidateIds(IReadOnlyList<long> ids)
    {
        if (ids.Count < 2 || ids.Count > MaximumFeatures || ids.Distinct().Count() != ids.Count)
            throw new InvalidOperationException($"Select between 2 and {MaximumFeatures} distinct point features for one shared label.");
    }

    private static IReadOnlyList<MapPoint> ReadAnchors(FeatureClass featureClass,
        IReadOnlyList<long> ids, SpatialReference mapReference, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var points = new Dictionary<long, MapPoint>();
        using var definition = featureClass.GetDefinition();
        cancellationToken.ThrowIfCancellationRequested();
        using var cursor = featureClass.Search(new QueryFilter
        {
            ObjectIDs = ids.ToArray(),
            SubFields = $"{definition.GetObjectIDField()},{definition.GetShapeField()}"
        }, false);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!cursor.MoveNext())
                break;
            cancellationToken.ThrowIfCancellationRequested();
            using var feature = (Feature)cursor.Current;
            if (feature.GetShape() is not MapPoint point || point.IsEmpty)
                throw new InvalidOperationException($"Source feature {feature.GetObjectID()} has no usable point geometry. No graphic was changed.");
            points.Add(feature.GetObjectID(), ProjectPoint(point, mapReference, cancellationToken));
        }
        cancellationToken.ThrowIfCancellationRequested();
        var missing = ids.Where(id => !points.ContainsKey(id)).ToArray();
        if (missing.Length > 0)
            throw new InvalidOperationException($"{missing.Length} source feature(s) are missing or inaccessible. No graphic was changed. Create a new label with the intended members.");
        return ids.Select(id => points[id]).ToArray();
    }

    private static MapPoint ProjectPoint(MapPoint point, SpatialReference mapReference,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (point.IsEmpty || point.SpatialReference is null || point.SpatialReference.IsUnknown)
            throw new InvalidOperationException("Every source point and the label position need a known coordinate system.");
        // Project() chooses its own default datum transformation; it does not honor
        // the map's configured transformations. Only projection changes within one
        // geographic coordinate system are supported until that context is handled.
        var sourceGcs = point.SpatialReference.Gcs;
        var targetGcs = mapReference.Gcs;
        if (sourceGcs is null || targetGcs is null || sourceGcs.IsUnknown || targetGcs.IsUnknown ||
            !sourceGcs.IsEqual(targetGcs))
            throw new InvalidOperationException(
                "The source and target use different geographic coordinate systems. This prototype does not apply geographic transformations. Project the source data into the map coordinate system using the intended transformation, then create a new shared label.");
        var projected = (MapPoint)GeometryEngine.Instance.Project(point, mapReference);
        cancellationToken.ThrowIfCancellationRequested();
        if (projected.IsEmpty || !double.IsFinite(projected.X) || !double.IsFinite(projected.Y))
            throw new InvalidOperationException("A point cannot be projected into the map's coordinate system.");
        return MapPointBuilderEx.CreateMapPoint(projected.X, projected.Y, mapReference);
    }

    private static string ReadCommonText(FeatureClass featureClass, IReadOnlyList<long> ids, string fieldName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var definition = featureClass.GetDefinition();
        var field = definition.GetFields().SingleOrDefault(candidate => candidate.Name == fieldName && candidate.FieldType == FieldType.String)
            ?? throw new InvalidOperationException("Choose an available text field from the source feature class.");
        var seen = new HashSet<long>();
        string? common = null;
        cancellationToken.ThrowIfCancellationRequested();
        using var cursor = featureClass.Search(new QueryFilter
        {
            ObjectIDs = ids.ToArray(),
            SubFields = $"{definition.GetObjectIDField()},{field.Name}"
        }, false);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!cursor.MoveNext())
                break;
            cancellationToken.ThrowIfCancellationRequested();
            using var row = cursor.Current;
            var value = row[field.Name] as string;
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException("The chosen field is blank for at least one source feature. Enter custom text or choose another field.");
            if (common is not null && !string.Equals(common, value, StringComparison.Ordinal))
                throw new InvalidOperationException("The chosen field has different values among the selected features. Enter custom text or choose a shared-value field.");
            common = value;
            seen.Add(row.GetObjectID());
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (seen.Count != ids.Count || common is null)
            throw new InvalidOperationException("Some source features are missing or inaccessible. Select the intended features again.");
        return common;
    }

    private static string GetSourceIdentity(FeatureLayer layer)
    {
        var connection = layer.GetDataConnection()
            ?? throw new InvalidOperationException("The source layer has no stable data connection to track.");
        var identity = connection.ToJson();
        // Store only a fingerprint, never connection strings, paths, or service tokens.
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
    }

    private static void ValidateSource(FeatureLayer layer, string identity)
    {
        if (string.IsNullOrEmpty(identity) || GetSourceIdentity(layer) != identity)
            throw new InvalidOperationException("The source layer now points to a different dataset. Create a new label to avoid linking unrelated object IDs.");
    }

    private static CIMStringMap Property(string key, string value) => new() { Key = Prefix + key, Value = value };
}
