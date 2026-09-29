using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ProMessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace LegendScaler;

internal sealed class ScaleLegendButton : Button
{
    private bool _busy;

    protected override void OnUpdate() => Enabled = !_busy && LayoutView.Active is not null;

    protected override async void OnClick()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var view = LayoutView.Active;
            if (view is null)
                throw new InvalidOperationException("Open a layout and select one legend.");

            var target = await QueuedTask.Run(() => LegendScaleService.Inspect(view));
            var window = new ScaleLegendWindow(view, target)
            {
                Owner = FrameworkApplication.Current.MainWindow
            };
            window.ShowDialog();
        }
        catch (Exception ex)
        {
            ProMessageBox.Show(ex.Message, "Legend Scaler");
        }
        finally
        {
            _busy = false;
        }
    }
}
