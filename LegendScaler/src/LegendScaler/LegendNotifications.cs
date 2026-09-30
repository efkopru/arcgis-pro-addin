using ArcGIS.Desktop.Framework;
using ProMessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace LegendScaler;

internal static class LegendNotifications
{
    public static void Show(Notification notification)
    {
        try
        {
            FrameworkApplication.AddNotification(notification);
        }
        catch (Exception notificationError)
        {
            // Reporting must not turn a completed edit into an apparent scaling
            // failure or replace the recovery guidance from a failed restoration.
            System.Diagnostics.Trace.TraceError($"Legend Scaler notification failed: {notificationError}");
            try
            {
                ProMessageBox.Show(notification.Message, notification.Title);
            }
            catch (Exception fallbackError)
            {
                // Pro may be shutting down. Preserve the original outcome in logs.
                System.Diagnostics.Trace.TraceError(
                    $"{notification.Title}: {notification.Message}\nFallback reporting failed: {fallbackError}");
            }
        }
    }
}
