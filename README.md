# ArcGIS Pro add-ins

Two independent C# / .NET add-ins for **ArcGIS Pro 3.7**, each contained in its own folder.

| Add-in | Purpose | Documentation |
| --- | --- | --- |
| Legend Scaler | Resize a layout legend's text, patches, spacing, and frame by a percentage. | [LegendScaler/README.md](LegendScaler/README.md) |
| Multiple Leaders | Place one shared text graphic with leader lines to selected point features. | [MultipleLeaders/README.md](MultipleLeaders/README.md) |

## Project layout

```text
arcgis-pro-addin/
  LegendScaler/
    LegendScaler.slnx
    Directory.Build.props
    NuGet.Config
    build.ps1
    README.md
    src/LegendScaler/
    tests/LegendScaler.Checks/
    artifacts/LegendScaler.esriAddinX
  MultipleLeaders/
    MultipleLeaders.slnx
    Directory.Build.props
    NuGet.Config
    build.ps1
    README.md
    src/MultipleLeaders/
    tests/MultipleLeaders.Checks/
    examples/
    artifacts/MultipleLeaders.esriAddinX
  VALIDATION.md
  ADDIN_IDEAS_AND_PLAN.md
  COMMUNITY_RESEARCH.md
```

Each add-in owns its build configuration, source, checks, and installer. Generated `artifacts/`, `bin/`, `obj/`, and `.tools/` folders are excluded from Git. Shared research and validation records remain at the repository root.

## Build

Requirements: Windows, ArcGIS Pro 3.7, and the .NET 10 SDK with Windows Desktop reference packs.

From the repository root:

```powershell
./LegendScaler/build.ps1
./MultipleLeaders/build.ps1
```

Alternatively, run `./build.ps1` inside either add-in's folder. Each script compiles that add-in, runs its standalone checks, validates its DAML, and produces its installer without installing it.

## Install

Close all ArcGIS Pro instances, then double-click the package for the add-in you want:

- [LegendScaler/artifacts/LegendScaler.esriAddinX](LegendScaler/artifacts/LegendScaler.esriAddinX)
- [MultipleLeaders/artifacts/MultipleLeaders.esriAddinX](MultipleLeaders/artifacts/MultipleLeaders.esriAddinX)

Packages are created locally by the build scripts. Use each add-in's README for instructions and known limitations. The [validation record](VALIDATION.md) separates automated checks from native behavior that still requires verification.
