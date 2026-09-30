# Legend Scaler

An initial C# / .NET add-in for **ArcGIS Pro 3.7** that scales an ordinary live layout legend by a percentage.

This add-in is self-contained under `LegendScaler/`, with its own solution, source, checks, build script, and generated installer. It does not depend on Multiple Leaders.

## Try the prototype

1. Build with `./build.ps1` from this directory, or use the existing `artifacts/LegendScaler.esriAddinX` package.
2. Close ArcGIS Pro, double-click the package, and install it using Esri's add-in installer. The prototype is unsigned; organization add-in policy may prevent installation.
3. Start Pro, open a project with a layout, and select exactly one legend in the layout Contents pane. Use an unlocked, unrotated legend outside a group.
4. Open **Legend Tools > Resize Legend**. Choose a preset or enter a percentage, such as `125` or `125%` for 25% larger. The dialog shows the size change and proposed frame width and height in the layout's units.
5. Leave **A new copy (keep the original)** selected and click **Create scaled copy**. It places a separate live legend to the right of the source. The copy may extend outside the page.
6. To change the source instead, choose **The selected legend** and click **Resize selected legend**. Use the layout's native Undo command to revert either action. A notification confirms completion and reports frame overflow.

**Exit:** click **Close**, the title-bar close button, or press **Esc**. Closing also cancels queued work. If an edit has already finished, use layout **Undo** to reverse it. This is a one-time command; there is no scaling mode left running after the dialog closes. To hide a resulting legend, clear its visibility checkbox in the layout Contents pane.

Version **0.1.3** adds percentage presets, live larger/smaller feedback, explicit copy/original options with one action button, layout units in the frame preview, clearer selection guidance, and completion notifications. Detected style notes are counted in the expandable section. The dialog defaults to 125% and a new copy each time.

The cancellation safeguards introduced in **0.1.2** remain: Close stays enabled, queued edits check cancellation, unfinished mutations attempt restoration, and closed windows ignore late success callbacks. Expected cancellation stays quiet. Unexpected failures after closing, including failed restoration, produce a persistent error notification with recovery guidance. If notification delivery fails, a message box retains that same guidance. An active-layout change is also rejected before mutation.

**Updating an installed version:** close **all** ArcGIS Pro instances before installing and reopening. An earlier attempt retained a stale cached 0.1.0 DLL despite the updated package. During the subsequent QA run, only these add-ins' stale cache directories were renamed into an ignored local QA backup. The Legend Scaler DLL inside the installed package and its regenerated cached copy were verified as **0.1.2.0**, with matching SHA-256 hashes. Native checks then ran in an isolated test setup; no user working project was edited. See [validation record](../VALIDATION.md).

The scale is relative to the currently selected legend. Applying 125% twice produces 156.25% of its starting size. Accepted input is 10% through 1,000%, with an optional trailing `%`; decimal input follows the current Windows culture. Enter `1000` without a thousands separator. At 100%, resizing the original is disabled, but creating a copy remains available.

## What this version implements

- A ribbon command and percentage dialog with 50%, 75%, 100%, 125%, 150%, and 200% presets, live size feedback, and proposed frame dimensions in layout units.
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

Verified locally for **0.1.3**: Pro 3.7.2 assemblies, .NET SDK 10.0.204, Release compilation with zero warnings/errors, 21 passing checks including percentage input and cancellation/restoration, valid DAML schema, and validated package contents. The cancellation checks exercise the actual edit wrapper, with simulated mutations; they do not establish native restoration behavior.

## Native verification

The **0.1.3 dialog changes have not been exercised in Pro**. Preset interaction, copy/original selection, layout-unit display, completion/overflow notifications, reporting a failed restoration after close, and keyboard exit remain native acceptance checks for this version. The evidence below belongs to **0.1.2**, before these dialog changes.

The following checks passed in **ArcGIS Pro 3.7.2**, using **Test Legend** in the isolated **Addin QA** test setup with the verified 0.1.2.0 DLL:

- **Close**, **Esc**, and the title-bar **X** each closed the idle dialog.
- **Scale original** at **125%** changed the frame shown on reinspection from **5.5 × 4.25** to **6.875 × 5.313** layout units. The latter height is rounded in the dialog.
- **Ctrl+Z**, followed by reinspection, restored the displayed frame to **5.5 × 4.25**.

These results establish dialog exit and the tested original-frame scaling/Undo behavior. They do not establish proportional rendering of every legend component or cancellation while a native edit is queued or running.

### Native acceptance checks still required

| Case | Expected result |
| --- | --- |
| 0.1.3 presets, typed percentages, and output choice | Correct live feedback and frame units; copy is the default; 100% permits copy but disables original resizing; notification identifies completion or overflow |
| Close, Esc, and title-bar X while work is queued or running | Dialog closes; canceled queued work does not edit a legend; unfinished mutations restore safely; ordinary cancellation stays quiet; failed restoration reports recovery guidance without reopening the resize dialog |
| Simple polygon, line, and point legends at 50%, 125%, and 200% | Fonts, gaps, patches, and frame change as specified; record any map-symbol mismatch |
| Mixed font sizes, hidden title, zero spacing | Relative text sizes remain correct; hidden/zero settings remain intact |
| Copy preview | Original unchanged; separate live legend appears beside it |
| Scale original, Undo, Redo across legend properties | Beyond the tested frame dimensions, verify fonts, fitting, anchor, and symbols restore; verify Redo |
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
- `src/LegendScaler/LegendScaleInput.cs`: culture-aware percentage input and relative-size feedback.
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
