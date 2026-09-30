# ArcGIS Pro add-ins

Two independent C# / .NET add-ins for **ArcGIS Pro 3.7**, each contained in its own folder.

| Add-in | Purpose | Documentation |
| --- | --- | --- |
| Legend Scaler | Resize a layout legend's text, patches, spacing, and frame by a percentage. | [LegendScaler/README.md](LegendScaler/README.md) |
| Multiple Leaders | Create, move, and resize one shared text graphic connected to selected point features. | [MultipleLeaders/README.md](MultipleLeaders/README.md) |

**Legend Tools** is the ribbon tab for Legend Scaler. Open a layout, select its legend, and choose a percentage. For example, 125% enlarges supported text, spacing, symbol patches, and frame dimensions by 25%. Apply it to the original legend or create a scaled copy. It does not change map zoom or feature labels; some symbol sizes remain controlled by the source renderer.

**Multiple Leaders** works in a map. **Create Shared Label** offers text and line previews, common sizes, and remembered style choices during the Pro session. Open **Manage Labels** to choose an existing label by its text and source layer, without first selecting a graphic. **Move Label** places it at your next click, **Resize Label** changes its font size, and **Reconnect Leaders** updates endpoints after source points move. The direct ribbon commands also open the label browser when no single editable label is selected.

Current packages: **Legend Scaler 0.1.3** and **Multiple Leaders 0.1.5**. Legend Scaler now has percentage presets, a live larger/smaller explanation, a frame preview in layout units, and an explicit copy/original choice. These usability revisions pass local build and standalone checks; the [validation record](VALIDATION.md) identifies the earlier versions used for native tests and the new workflows still awaiting them.

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
