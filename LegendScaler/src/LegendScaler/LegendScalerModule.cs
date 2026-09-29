using ArcGIS.Desktop.Framework.Contracts;

namespace LegendScaler;

internal sealed class LegendScalerModule : Module
{
    protected override bool CanUnload() => true;
}
