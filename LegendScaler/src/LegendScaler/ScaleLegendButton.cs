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
            if (window.CompletedResult is { } result)
            {
                var details = result.IsCopy
                    ? $"Created {result.Name} beside the original."
                    : $"Resized {result.Name} to {window.AppliedPercent:0.##}% of its previous size.";
                LegendNotifications.Show(new Notification(Notification.NotificationLevel.Project,
                    result.FitsFrame ? NotificationType.Confirmation : NotificationType.Warning)
                {
                    Severity = result.FitsFrame ? Notification.SeverityLevel.Low : Notification.SeverityLevel.High,
                    Title = result.FitsFrame ? (result.IsCopy ? "Legend copy created" : "Legend resized") : "Legend resized: check the frame",
                    Message = details + (result.FitsFrame
                        ? " Use layout Undo (Ctrl+Z) to reverse the change."
                        : " The legend does not fit its frame. Enlarge the frame or use layout Undo (Ctrl+Z).")
                });
            }
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
