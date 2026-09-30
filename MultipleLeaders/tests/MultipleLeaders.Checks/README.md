# Multiple Leaders checks

From the `MultipleLeaders` folder, run:

```powershell
dotnet run --project tests/MultipleLeaders.Checks/MultipleLeaders.Checks.csproj --configuration Release
```

Requires the .NET 10 SDK and locally installed ArcGIS Pro 3.7. The harness links the production `CalloutGraphicBuilder.cs`, `CalloutInput.cs`, `PlacementLifetime.cs`, and `PlacementExitScheduler.cs`, references `ArcGIS.Core.dll`, and uses no downloaded packages or Desktop UI assemblies. Failures return a nonzero exit code. `build.ps1` runs the checks before packaging.

The 27 checks include cancellation before the first click, concurrent clicks admitting only one placement, cancellation before a delayed action runs while a newer request remains usable, and deferred tool exits with stale, reentrant, and failed transitions. They do not establish MapTool event dispatch or window closing in Pro.

Verified scope: direct leader endpoint and label-position coordinates, native leader types, symbol dimensions, JSON text/style/leader-type metadata, independent graphics, duplicate coordinates, input limits, literal text handling, and early refresh validation. Dialog input checks cover decimal-point and decimal-comma cultures, rejected grouping and foreign separators, nonfinite numbers, accepted boundaries, and shared text validation before placement. Text normalization remains unescaped until CIM construction.

**These checks do not verify geometry persistence.** In this unhosted console process, CIM JSON omits point geometry and geometry cloning needs the native CoreInterop runtime. The harness therefore makes no geometry round-trip or successful-refresh claim. Native rendering, placement, feature projection, successful Reconnect Leaders, save/reopen, and undo require the integration procedure in ArcGIS Pro.
