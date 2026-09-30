using ArcGIS.Desktop.Framework.Contracts;

namespace MultipleLeaders;

internal sealed class RefreshCalloutButton : Button
{
    protected override void OnUpdate() => Enabled = CalloutPlacement.CanStart;
    protected override async void OnClick() => await CalloutEditWorkflow.RunAsync(CalloutEditAction.Reconnect);
}
