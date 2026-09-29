using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ProMessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace MultipleLeaders;

internal sealed class RefreshCalloutButton : Button
{
    private bool _busy;

    protected override void OnUpdate() => Enabled = !_busy && MapView.Active is not null &&
        CalloutPlacement.Current is null && !CalloutPlacement.IsExiting;

    protected override async void OnClick()
    {
        if (_busy || CalloutPlacement.Current is not null || CalloutPlacement.IsExiting) return;
        _busy = true;
        try
        {
            var view = MapView.Active ?? throw new InvalidOperationException("Open the map containing the callout.");
            await QueuedTask.Run(() => CalloutService.RefreshSelected(view));
            FrameworkApplication.AddNotification(new Notification
            {
                Title = "Leaders reconnected",
                Message = "Leader endpoints now match the original source points. Label position, text, and formatting were preserved."
            });
        }
        catch (Exception ex)
        {
            ProMessageBox.Show(ex.Message, "Multiple Leaders");
        }
        finally { _busy = false; }
    }
}
