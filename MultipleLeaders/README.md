# Multiple Leaders

An independent C# / .NET add-in for **ArcGIS Pro 3.7**. Select point features, place one text graphic, and connect that text to each distinct point location with native leader lines.

Everything for this add-in is under `MultipleLeaders/`. It has its own solution, source, checks, build script, and installer, and does not depend on Legend Scaler.

## Install and use

1. Run `./build.ps1` from this folder to create `artifacts/MultipleLeaders.esriAddinX`. Close ArcGIS Pro and double-click that package. The package is unsigned; organization add-in settings may restrict its installation.
2. Reopen Pro and activate a **2D map** with a known coordinate system.
3. Select **2–100 point features in exactly one feature layer**. Clear selections in other layers. At least two distinct point locations are required.
4. Open **Multiple Leaders > Create Shared Label**. The dialog illustrates how several points connect to one label.
5. Enter the shared text, or select a string field and click **Use shared value**. The field must have the same nonblank value for every selected feature.
6. Set the font size and leader width, then click **Next: place on map**. Click once at the desired label position. A cursor tip explains placement and Escape.

The add-in creates one text graphic in a graphics layer named **Multiple Leaders**. The tool returns to Explore after one placement. The source features and their attributes are not changed. Repeated coincident points produce one visible leader for that location.

**Stop placing:** press **Esc** in the map, click **Cancel Placement**, or select another tool. Cancel and the title-bar X close the text dialog, including during a shared-field lookup. Cancellation prevents a queued creation from starting; cancellation during a mutation attempts to restore the prior state. An already completed label remains on the map.

**Hide the result:** clear the **Multiple Leaders** layer's visibility checkbox in Contents. Use **Undo** to reverse creation. These controls do not turn automatic labeling on or off; this add-in creates manually placed graphics.

Version **0.1.2** moves text entry before map placement, adds explicit cancellation and Escape handling, and prevents duplicate clicks or stale queued requests from creating another label.

Close **all** Pro instances when updating, including another project window. The native 0.1.2 test attempt encountered cached 0.1.0 DLLs still held open by Pro. New ribbon configuration alone does not prove the new code loaded. See the [validation record](../VALIDATION.md).

## Refresh and editing

Select the created graphic using Pro's graphic selection tools or the graphics-layer Contents entries, then choose **Reconnect Leaders**. The command re-reads the original feature IDs and updates leader endpoints, preserving the text position, displayed text, and styling.

Refresh is explicit. It does not automatically respond to feature edits, and it does not recalculate a previously copied field value. Missing source features cause refresh to fail without dropping members. A changed data connection also causes refusal. Create a new label to change membership.

Pro's native graphic formatting tools can edit the text and callout style. If moving the graphic also moves its leader endpoints, **Reconnect Leaders** reconnects them to the original features. Creation and refresh are grouped into named map Undo operations; their full behavior still needs native verification.

The graphic stores the source layer URI, ObjectIDs, a hash of the data connection, and creation metadata. Refresh requires the original layer and dataset. Replacing a dataset at the same connection while reusing its ObjectIDs cannot be reliably detected in this prototype.

## Scope

- One native `CIMTextGraphic` with a `CIMLeaderPoint` per distinct selected location.
- A centered black text symbol and native line callout, without a background rectangle.
- Manual text or a shared string-field value. Font size 6–72 points; line width 0.1–5 points; text up to 500 characters.
- Literal text handling for `<`, `>`, and `&`, so attribute contents are not interpreted as formatting instructions.
- Projection of source points to the map coordinate system for creation, and to the existing graphic coordinate system for refresh.
- Projection changes require the same underlying geographic coordinate system. Cross-GCS inputs are rejected; preproject those data using the intended datum transformation before creating labels.
- Rechecks source membership and data connection before changing a graphic.

**This implements manually placed shared-label graphics.** It does not extend Maplex or provide automatic clustering, collision avoidance, scale-dependent regrouping, or feature-linked annotation. The original automatic-labeling request is broader than this first version.

Point feature layers and 2D maps are the initial scope. Multipoints, lines, polygons, scenes, joined-field lookup, automatic field-text refresh, and moved/grouped-label edge cases are outside the qualified scope. Locked or grouped graphics are rejected by refresh. Leader crossings are possible and must be resolved through label placement.

## Build

Requirements: Windows, ArcGIS Pro 3.7, and the .NET 10 SDK with Windows Desktop reference packs.

From this folder:

```powershell
./build.ps1
# Optional nondefault Pro installation:
./build.ps1 -ArcGISProInstallDir 'D:\ArcGIS\Pro'
```

The build uses local Pro assemblies, validates the DAML against the installed schema, runs the standalone checks, and packages `artifacts/MultipleLeaders.esriAddinX`. It does not install the package. It excludes Esri runtime assemblies and preserves a prior installer until the new build and checks succeed.

Verified locally against Pro 3.7.2: compilation with zero warnings/errors and 15 passing standalone checks, including cancellation before a click, duplicate/concurrent clicks, and cancellation before a delayed worker runs. These exercise the placement lifetime, not native MapTool keyboard dispatch. Full in-Pro acceptance remains pending.

Open `MultipleLeaders.slnx` in Visual Studio to develop both the add-in and its check harness.

## Native verification still required

Standalone checks inspect the created CIM text, leader types, direct endpoint coordinates, style values, and invalid input handling. **Geometry serialization, positive refresh, UI rendering, Undo, PDF export, and project save/reopen require ArcGIS Pro's native runtime.** Passing standalone checks does not establish those behaviors.

| Test | Expected behavior |
| --- | --- |
| Cancel, Esc, and title-bar X in the text dialog | Dialog closes, including during field lookup; no placement starts |
| Esc before the first map click; Cancel Placement; switch to Explore | No graphic is created; placement stops |
| Cancel while creation waits in the worker queue | Delayed creation does not add a graphic |
| Double-click, cancel, then start again | At most one label per request; old continuations do not affect the new request |
| Three selected points, one clicked label location | One label, three leaders terminating at the points |
| Coincident points plus another point | One leader per distinct XY location |
| Text containing `A & B <test>` | Literal characters display; no formatting is injected |
| Common string value versus mixed/blank values | Shared text accepted only for a common nonblank value |
| Source layer and map use different projections of the same GCS | Leaders terminate at the displayed feature locations |
| Source layer and map use different geographic coordinate systems | Clear rejection before any graphic is created |
| Move a source point, select graphic, refresh | Endpoint moves to the feature; text position stays fixed |
| Move the graphic, then refresh | Label stays at its new location; anchors return to source features |
| Delete a source point or repoint the layer | Refresh rejects the change and leaves the graphic intact |
| Undo/Redo creation and refresh | Each named operation reverses/reapplies the complete change |
| Save/reopen, refresh again, export a layout to PDF | Text, leader anchors, and source metadata persist |

## Synthetic demo

`examples/create_demo.py` creates an isolated demo geodatabase and a new map in the open project. It uses invented point coordinates and does not overwrite existing data. Run it from Pro's Python window:

```python
import runpy
runpy.run_path(r"C:\path\to\arcgis-pro-addin\MultipleLeaders\examples\create_demo.py", run_name="__main__")
```

Replace the example path with your checkout location. The three selected Alpha points share `GROUP_NAME = Alpha`. Use Create Shared Label, choose that field, click Next: place on map, then click near the points. The demo script itself must also be run and verified inside Pro.

## Source

- `src/MultipleLeaders/CalloutGraphicBuilder.cs`: native text and leader construction, validation, and anchor replacement.
- `src/MultipleLeaders/CalloutService.cs`: source selection, field lookup, coordinate projection, provenance, graphic creation, and refresh.
- `src/MultipleLeaders/PlaceCalloutTool.cs`: map-click placement.
- `src/MultipleLeaders/CalloutOptionsWindow.xaml`: label and style options.
- `tests/MultipleLeaders.Checks/`: standalone checks and their limits.

## References

- [Community request: multiple leaders from a single label](https://community.esri.com/en/discussion/931928/multiple-leaders-from-a-single-label-i-e-label-clustering)
- [CIMTextGraphic members](https://doc.esri.com/en/arcgis-pro/latest/sdk/api-reference/topic5721.html)
- [Leaders property](https://doc.esri.com/en/arcgis-pro/latest/sdk/api-reference/topic5737.html)
- [Esri callout and native leader example](https://community.esri.com/en/discussion/1192786/callout-leader-line)
- [Pro text formatting rules](https://doc.esri.com/en/arcgis-pro/latest/help/mapping/text/text-formatting-tags.html)
