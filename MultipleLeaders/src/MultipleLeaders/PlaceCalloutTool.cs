using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ProMessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace MultipleLeaders;

internal sealed class PlaceCalloutTool : MapTool
{
    private bool _busy;

    public PlaceCalloutTool()
    {
        IsSketchTool = true;
        SketchType = SketchGeometryType.Point;
        SketchOutputMode = SketchOutputMode.Map;
        UseSnapping = false;
    }

    protected override async Task<bool> OnSketchCompleteAsync(Geometry geometry)
    {
        if (_busy) return true;
        _busy = true;
        try
        {
            var view = MapView.Active;
            if (view is null || geometry is not MapPoint location)
                throw new InvalidOperationException("Open a 2D map and click a label position.");

            var selection = await QueuedTask.Run(() => CalloutService.Capture(view));
            var window = new CalloutOptionsWindow(selection)
            {
                Owner = FrameworkApplication.Current.MainWindow
            };
            if (window.ShowDialog() != true) return true;

            if (!ReferenceEquals(view, MapView.Active))
                throw new InvalidOperationException("The active map changed. Select the points and place the label again.");

            var options = window.Options!;
            await QueuedTask.Run(() => CalloutService.Create(view, selection, location,
                options.Text, options.FontSize, options.LineWidth, options.SourceField));
            return true;
        }
        catch (Exception ex)
        {
            ProMessageBox.Show(ex.Message, "Multiple Leaders");
            return true;
        }
        finally
        {
            _busy = false;
            // Single-use placement avoids accidentally creating another callout
            // while the user is trying to inspect the first one.
            await FrameworkApplication.SetCurrentToolAsync("esri_mapping_exploreTool");
        }
    }
}
