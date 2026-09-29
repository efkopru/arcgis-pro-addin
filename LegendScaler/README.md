# Legend Scaler

An initial C# / .NET add-in for **ArcGIS Pro 3.7** that scales an ordinary live layout legend by a percentage.

This add-in is self-contained under `LegendScaler/`, with its own solution, source, checks, build script, and generated installer. It does not depend on Multiple Leaders.

## Try the prototype

1. Build with `./build.ps1` from this directory, or use the existing `artifacts/LegendScaler.esriAddinX` package.
2. Close ArcGIS Pro, double-click the package, and install it using Esri's add-in installer. The prototype is unsigned; organization add-in policy may prevent installation.
3. Start Pro, open a project with a layout, and select exactly one legend in the layout Contents pane. Use an unlocked, unrotated legend outside a group.
4. Open **Legend Tools > Resize Legend**. Enter a percentage, such as `125` for 25% larger or `50` for half size.
5. Use **Create scaled copy** first. It places a separate live legend beside the source, which remains unchanged. The copy may extend outside the page.
6. **Scale original** modifies the selected legend. Use the layout's native Undo command to revert the operation.

**Exit:** click **Close**, the title-bar close button, or press **Esc**. Closing also cancels queued work. If an edit has already finished, use layout **Undo** to reverse it. This is a one-time command; there is no scaling mode left running after the dialog closes. To hide a resulting legend, clear its visibility checkbox in the layout Contents pane.

Version **0.1.2** fixes dialogs that disabled closing while work was queued. Close remains enabled, queued edits check cancellation, unfinished mutations attempt restoration, and closed windows ignore late completion callbacks. The dialog now explains the percentage, copy versus original, and Undo; technical caveats are in an expandable section.

**Updating an installed version:** close **all** ArcGIS Pro instances before installing and reopening. During the 0.1.2 native test attempt, open Pro processes retained cached 0.1.0 DLLs while reading the new ribbon configuration. New commands were disabled, and replacing those DLLs failed because another process held them open. This was not a successful runtime test of 0.1.2. See [validation record](../VALIDATION.md).

The scale is relative to the currently selected legend. Applying 125% twice produces 156.25% of its starting size. Accepted input is 10% through 1,000%; decimal input follows the current Windows culture. At 100%, scaling the original is disabled, but creating a copy remains available.

## What this version implements

- A ribbon command and percentage dialog with the proposed frame dimensions.
- Scaling of explicit font sizes, legend gaps, patches, item indents, and the frame about its existing anchor.
- Scaling of supported text halos, frame borders, shadows, and simple symbol effects contained in the legend definition.
- A live copied legend for comparison, or a grouped native undo operation for changes to the original.
- Fixed font sizes through **Adjust columns** fitting with automatic font sizing disabled. Column wrapping can change. Undo restores the original fitting settings.
- Input/selection checks, unsupported-style warnings, overflow reporting, and attempted restoration on mutation failure.

The underlying map, layer renderers, labels, and datasets are not edited. Linkage and other non-size settings are retained by cloning the legend definition.

## Boundaries of this prototype

**This is not yet a guarantee that every visible mark scales proportionally.** Point-symbol sizes and line strokes inherited from map renderers can retain their map size even when their legend patches grow. Existing `ScaleSymbols` and `ScaleToPatch` settings are retained. Complex fills, symbol overrides, inline text formatting, callout geometry, and some effects can also override or escape the transform. The dialog reports detected limitations.

Percentages apply to the configured CIM sizes. If Pro had automatically shrunk the source text to fit, its visible starting size can differ from that configured size. This case requires particular attention during the native preview check.

Native resizing handles do not invoke this command. Grouped or rotated legends are rejected. Raster ramps, chart legends, map-series behavior, and complex symbol styles have not been qualified. Rendering can reflow the legend, and copy placement does not automatically expand the page.

## Build and automated checks

Requirements: Windows, the **.NET 10 SDK** including its Windows Desktop reference pack, and **ArcGIS Pro 3.7**. Visual Studio and its SDK extension are not required by this command-line build.

From this folder:

```powershell
./build.ps1
# For a different Pro install location:
./build.ps1 -ArcGISProInstallDir 'D:\ArcGIS\Pro'
```

The build uses the local Pro assemblies and an offline NuGet configuration. It compiles in Release, runs standalone checks against real CIM objects, validates `Config.daml` against Esri's installed schema, and packages only this add-in's files. It does not install the add-in. An existing package is replaced only after the checks succeed. Build caches remain inside `.tools/`.

The standalone checks validate the transformation and preservation of CIM properties. They **do not prove** in-app rendering, native undo, PDF export, or save/reopen behavior. Those require the native acceptance checks below.

Verified locally: Pro 3.7.2 assemblies, .NET SDK 10.0.204, Release compilation with zero warnings/errors, 17 passing checks including cancellation/restoration, valid DAML schema, and validated package contents. The cancellation checks exercise the actual edit wrapper, with simulated mutations; they do not establish native restoration behavior. In-app acceptance checks remain pending.

## Native acceptance checks still required

| Case | Expected result |
| --- | --- |
| Close, Esc, and title-bar X, both idle and while work is queued | Dialog closes; canceled queued work does not edit a legend; no late dialog reappears |
| Simple polygon, line, and point legends at 50%, 125%, and 200% | Fonts, gaps, patches, and frame change as specified; record any map-symbol mismatch |
| Mixed font sizes, hidden title, zero spacing | Relative text sizes remain correct; hidden/zero settings remain intact |
| Copy preview | Original unchanged; separate live legend appears beside it |
| Scale original, Undo, Redo | One undo restores fonts, frame, fitting, anchor, and symbols; redo reapplies |
| Different element anchors; aspect-ratio lock on/off | Anchor position and original lock setting survive scaling |
| Save/reopen and export to PDF | Scaled appearance persists and matches the layout |
| Rename a map layer or change its classes | Both live legends still follow their map connection |
| Long labels and insufficient frame | Overflow is reported; no silent text shrink |
| Invalid selection, group, lock, or rotation | Helpful rejection without modifying the layout |

The first product decision after those checks is whether patch-based scaling is sufficient or whether a separate strategy is needed to reproduce map-symbol sizes within a live legend.

## Source layout

- `src/LegendScaler/LegendScaling.cs`: detached CIM transformation and warnings.
- `src/LegendScaler/LegendScaleService.cs`: layout changes, copy placement, fitting, undo grouping, and recovery.
- `src/LegendScaler/ScaleLegendWindow.xaml`: percentage dialog.
- `tests/LegendScaler.Checks/`: standalone CIM regression checks.
- `build.ps1`: build, checks, schema validation, and package creation.

## Engineering references

- [Original community request](https://community.esri.com/en/discussion/928723/scale-legend-contents-automatically-with-change-in-size)
- [Legend item properties](https://doc.esri.com/en/arcgis-pro/latest/sdk/api-reference/topic2727.html)
- [Legend fitting strategies](https://doc.esri.com/en/arcgis-pro/latest/help/layouts/layout-fitting-strategies.html)
- [Legend item symbol sizing](https://doc.esri.com/en/arcgis-pro/latest/help/layouts/work-with-legend-items.html)
- [Layout CopyElements](https://doc.esri.com/en/arcgis-pro/latest/sdk/api-reference/topic30743.html)
- [Composite operations](https://doc.esri.com/en/arcgis-pro/latest/sdk/api-reference/topic10199.html)

Packaging follows the root `Config.daml` and `Install/` binary layout in Esri's `Esri.ArcGISPro.Extensions30` 3.7.0.1901 build targets. No Esri runtime DLLs are redistributed.
