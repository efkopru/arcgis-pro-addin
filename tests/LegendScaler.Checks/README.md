# Legend scaling regression checks

Run from the repository root with ArcGIS Pro 3.7 and .NET 10 installed:

```powershell
dotnet run --project tests/LegendScaler.Checks/LegendScaler.Checks.csproj --configuration Release
```

The console harness links the production `LegendScaling.cs` and references the locally installed `ArcGIS.Core.dll`. It uses no test packages, no `ArcGIS.Desktop.*` assemblies, no Pro UI, and no license initialization. A failed check returns a nonzero exit code. Override `ArcGISProInstallDir` through MSBuild if Pro is installed in a different directory.

The checks cover immutable source definitions, deep-clone independence, mixed text sizes, default and item dimensions, zero and inherited dimensions, signed offsets, percentage properties, point versus multiplier line spacing, frame stroke/dash scaling, preserved layer and layout behavior, factor validation, repeated/inverse scaling, and limitation warnings.

These are synthetic CIM transformation checks. They do not establish visual fidelity, native legend fitting behavior, layout geometry updates, operation undo, add-in loading, PDF export, or project save/reopen behavior. Those require the manual checks in the repository documentation inside ArcGIS Pro.
