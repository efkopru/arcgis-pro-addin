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
        var snapshot = new SelectionSnapshot(map, layer, layer.URI, layer.Name, ids,
            ReadAnchors(featureClass, ids, map.SpatialReference, cancellationToken), fields)
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

        GraphicsLayer? target = map.GetLayersAsFlattenedList().OfType<GraphicsLayer>()
            .FirstOrDefault(layer => layer.Name == LayerName &&
                (layer.GetElements().Count == 0 || layer.GetElementsAsFlattenedList()
                    .Any(element => element.GetCustomProperty(Prefix + "Version") == SchemaVersion)));
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
                else if (!target.IsVisible)
                    throw new InvalidOperationException("Make the Multiple Leaders graphics layer visible before creating another label.");

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

    public static string RefreshSelected(MapView view, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var map = ValidateView(view);
        var selected = map.GetLayersAsFlattenedList().OfType<GraphicsLayer>()
            .SelectMany(layer => layer.GetSelectedElements()).ToArray();
        if (selected.Length != 1 || selected[0] is not GraphicElement element ||
            element.GetCustomProperty(Prefix + "Version") != SchemaVersion)
            throw new InvalidOperationException("Select exactly one shared-label graphic created by Multiple Leaders in the Contents pane.");
        if (element.IsLocked)
            throw new InvalidOperationException("Unlock the shared-label graphic before refreshing its leaders.");
        if (element.GetParent(false) is GroupElement)
            throw new InvalidOperationException("Ungroup the shared-label graphic before refreshing its leaders.");

        var sourceUri = element.GetCustomProperty(Prefix + "SourceLayerUri");
        var sourceIdentity = element.GetCustomProperty(Prefix + "SourceIdentity");
        var source = FindSource(map, sourceUri);
        long[] ids;
        try
        {
            ids = JsonSerializer.Deserialize<long[]>(element.GetCustomProperty(Prefix + "ObjectIds"))
                ?? throw new InvalidOperationException("Missing source object IDs.");
        }
        catch (JsonException error)
        {
            throw new InvalidOperationException("The graphic's saved source object IDs are invalid.", error);
        }
        ValidateIds(ids);
        using var featureClass = OpenPointClass(source);
        ValidateSource(source, sourceIdentity);
        if (element.GetGraphic() is not CIMTextGraphic before || before.Shape is not MapPoint position ||
            position.SpatialReference is null || position.SpatialReference.IsUnknown)
            throw new InvalidOperationException("The selected graphic is no longer a supported point text graphic.");
        // Preserve the graphic's own coordinate system if the map's system changed.
        var anchors = ReadAnchors(featureClass, ids, position.SpatialReference, cancellationToken);
        var updated = CalloutGraphicBuilder.UpdateAnchors(before, anchors);
        cancellationToken.ThrowIfCancellationRequested();
        map.OperationManager.CreateCompositeOperation(() =>
        {
            var mutationAttempted = false;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                mutationAttempted = true;
                element.SetGraphic(updated);
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch
            {
                if (mutationAttempted)
                    element.SetGraphic(before);
                throw;
            }
        }, "Refresh shared label leaders");
        return element.Name;
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
