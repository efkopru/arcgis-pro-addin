# Multiple Leaders checks

From the `MultipleLeaders` folder, run:

```powershell
dotnet run --project tests/MultipleLeaders.Checks/MultipleLeaders.Checks.csproj --configuration Release
```

Requires the .NET 10 SDK and locally installed ArcGIS Pro 3.7. The harness links the production `CalloutGraphicBuilder.cs`, references `ArcGIS.Core.dll`, and uses no downloaded packages or Desktop UI assemblies. Failures return a nonzero exit code. `build.ps1` runs the checks before packaging.

Verified scope: direct leader endpoint and label-position coordinates, native leader types, symbol dimensions, JSON text/style/leader-type metadata, independent graphics, duplicate coordinates, input limits, literal text handling, and early refresh validation.

**These checks do not verify geometry persistence.** In this unhosted console process, CIM JSON omits point geometry and geometry cloning needs the native CoreInterop runtime. The harness therefore makes no geometry round-trip or successful-refresh claim. Native rendering, placement, feature projection, successful Refresh Leaders, save/reopen, and undo require the integration procedure in ArcGIS Pro.
