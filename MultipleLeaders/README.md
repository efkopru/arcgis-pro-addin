# Multiple Leaders

An independent C# / .NET add-in for **ArcGIS Pro 3.7**. Select point features, place one text graphic, and connect that text to each distinct point location with native leader lines. Current version: **0.1.5**.

Everything for this add-in is under `MultipleLeaders/`. It has its own solution, source, checks, build script, and installer, and does not depend on Legend Scaler.

## Install and use

1. Run `./build.ps1` from this folder to create `artifacts/MultipleLeaders.esriAddinX`. Close ArcGIS Pro and double-click that package. The package is unsigned; organization add-in settings may restrict its installation.
2. Reopen Pro and activate a **2D map** with a known coordinate system.
3. Select **2–100 point features in exactly one feature layer**. Clear selections in other layers. At least two distinct point locations are required.
4. Open **Multiple Leaders > Create Shared Label**. The dialog previews the text size and leader width; size presets provide starting points.
5. Enter the shared text, or select a string field and click **Use shared value**. The field must have the same nonblank value for every selected feature.
6. Set the font size and leader width, then click **2. Place on map**. Click once at the desired label position. A cursor tip explains placement and Escape. The dialog remembers the last accepted sizes for this Pro session; its preview is illustrative, not the map's final rendering.

The add-in creates one text graphic in a graphics layer named **Multiple Leaders**. The tool returns to Explore after one placement. The source features and their attributes are not changed. Repeated coincident points produce one visible leader for that location.

**Stop placing:** press **Esc** in the map, click **Cancel Placement**, or select another tool. Cancel and the title-bar X close the text dialog, including during a shared-field lookup. Cancellation prevents a queued creation from starting; cancellation during a mutation attempts to restore the prior state. An already completed label remains on the map.

**Hide the result:** clear the **Multiple Leaders** layer's visibility checkbox in Contents. Use **Undo** to reverse creation. These controls do not turn automatic labeling on or off; this add-in creates manually placed graphics.

Version **0.1.5** adds **Manage Labels**, direct-command fallback to its label list, style previews and presets, and early checks for coincident source locations and hidden target layers. Native testing of these new workflows remains pending.

Close **all** Pro instances when updating, including another project window. A stale cache previously retained an older DLL despite updated ribbon configuration. Native tests verify the extracted DLL against the package, not only the ribbon captions. See the [validation record](../VALIDATION.md).

## Refresh and editing

Open **Multiple Leaders > Manage Labels** and choose a row by its text, source layer, and point count. Use **Move label**, **Resize label**, or **Reconnect leaders** in that window. No graphics selection is required. The list includes hidden labels and explains unsupported or missing-source entries. A missing source layer disables movement and reconnection; resizing remains available when the graphic itself is editable.

The ribbon's direct editing commands use one already selected editable graphic. If selection is missing, ambiguous, or unsupported, they open Manage Labels. **Graphics > Select** remains an optional way to select a label directly.

- **Move Label:** click the new position on the map. The label moves and its leaders reconnect to the current positions of the original source features. Text and styling are preserved. Esc or Cancel Placement cancels the move.
- **Resize Label:** enter a font size from **6 to 72 points**, or choose a preset. The dialog previews the size change and disables applying an unchanged size. Label position, leader endpoints, and leader width are preserved.
- **Reconnect Leaders:** use this after a source point has actually moved. It re-reads the original feature IDs, updates the endpoints, and reports completion in the manager or a notification. Label position, displayed text, and styling stay unchanged. If the endpoints already match the source points, there is no visible change.

Manage Labels stays open after resizing or reconnection. It reports progress and errors, disables overlapping actions, and keeps **Close**, **Esc**, and the title-bar X available. Closing cancels queued work; an edit that has already completed remains available through Undo. Move closes the manager before starting the next-click tool.

Reconnection is explicit and does not refresh text copied from a field. Missing source features or a changed data connection cause refusal without dropping members. Create a new label to change source membership. Pro's native graphic formatting tools remain available for editing text and callout style.

Creation appeared in the 0.1.3 native Undo history as **Create shared label with multiple leaders** and a separate **( Elements )** entry. Undo/Redo removed and restored the test graphic. Version 0.1.4 passed single-step resize and move Undo/Redo. Single-step creation Undo and Undo for reconnect remain unverified.

The graphic stores the source layer URI, ObjectIDs, a hash of the data connection, and creation metadata. Refresh requires the original layer and dataset. Replacing a dataset at the same connection while reusing its ObjectIDs cannot be reliably detected in this prototype.

## Scope

- One native `CIMTextGraphic` with a `CIMLeaderPoint` per distinct selected location.
- A centered black text symbol and native line callout, without a background rectangle.
- Manual text or a shared string-field value. Font size 6–72 points; line width 0.1–5 points; text up to 500 characters.
- Literal text handling for `<`, `>`, and `&`, so attribute contents are not interpreted as formatting instructions.
- Projection of source points to the map coordinate system for creation, and to the existing graphic coordinate system for reconnection and movement.
- Projection changes require the same underlying geographic coordinate system. Cross-GCS inputs are rejected; preproject those data using the intended datum transformation before creating labels.
- Move Label additionally requires the map and graphics layer to use the same coordinate system. If the map CRS changed after creating the graphics layer, restore that CRS before moving its labels with this command.
- New labels reuse a recognized graphics layer only when its known coordinate system matches the current map. Otherwise, a new Multiple Leaders graphics layer is created. Source geometry passed to the native creation API remains in map coordinates.
- Rechecks source membership and data connection before changing a graphic.

**This implements manually placed shared-label graphics.** It does not extend Maplex or provide automatic clustering, collision avoidance, scale-dependent regrouping, or feature-linked annotation. The original automatic-labeling request is broader than this first version.

Point feature layers and 2D maps are the initial scope. Multipoints, lines, polygons, scenes, joined-field lookup, and automatic field-text refresh are outside the qualified scope. Editing commands reject locked or grouped graphics. Leader crossings are possible and must be resolved through label placement.

## Build

Requirements: Windows, ArcGIS Pro 3.7, and the .NET 10 SDK with Windows Desktop reference packs.

From this folder:

```powershell
./build.ps1
# Optional nondefault Pro installation:
./build.ps1 -ArcGISProInstallDir 'D:\ArcGIS\Pro'
```

The build uses local Pro assemblies, validates the DAML against the installed schema, runs the standalone checks, and packages `artifacts/MultipleLeaders.esriAddinX`. It does not install the package. It excludes Esri runtime assemblies and preserves a prior installer until the new build and checks succeed.

The standalone harness checks graphic construction, creation input validation, literal text, and placement cancellation/scheduling. Native selection, manager actions, projection, rendering, and persistence require testing inside Pro. See the [validation record](../VALIDATION.md) for the current build and check results.

Open `MultipleLeaders.slnx` in Visual Studio to develop both the add-in and its check harness.

## Native verification

In the isolated synthetic test project, version **0.1.3** passed:

- Esc before the first click with sketch-tip focus, and Cancel Placement. Both returned to idle without adding a graphic and allowed another placement request.
- Switching to Pro's Explore tool canceled placement and restored idle controls without creating the canceled label.
- Shared-field lookup and creation of one Buildings label with three visible leaders terminating at the selected points.
- Automatic exit after creation, with Create Shared Label enabled and Cancel Placement disabled.
- Hiding the graphics layer hid the text and leaders.
- Named Undo/Redo history entries removed and restored the graphic, subject to the separate-entry limitation above.

Options-dialog Cancel and Esc were verified in 0.1.2; that dialog code is unchanged in 0.1.3. See [validation details](../VALIDATION.md) for the failed 0.1.2 placement test and remaining coverage.

In a further 0.1.3 check, Reconnect Leaders completed without an error when the endpoints already matched the source points. That produced no visible change. No spatial-reference failure was reproduced.

Native checks in 0.1.4 verified reconnecting after reopening the project with one source point moved from (50, 80) to (75, 100), preserving text position. Resize changed the existing label from 12 to 18 points while retaining leader endpoints; Ctrl+Z and Ctrl+Y reversed and reapplied the resize. The saved CIM retained the edited text, 18-point size, 2-point leader width, and all three correct source endpoints. Move Label relocated the text to the clicked map position, preserved the font and source endpoints, and returned to idle. One Undo restored its previous position. Escape canceled a pending move without changing the label.

### Further native acceptance

Standalone checks inspect CIM text, leader types, endpoint coordinates, style values, and invalid input handling. Native checks cover the simple cases above. Broader styles, projections, interrupted edits, complete Undo behavior, and PDF export still need qualification.

| Test | Expected behavior |
| --- | --- |
| Open Manage Labels with no graphics selected | Labels are listed by text, source, and member count; a row can be edited without Graphics > Select |
| Run a direct edit command with no valid single selection | Manage Labels opens instead of a selection-only error |
| Close or Esc while the manager is loading or waiting to edit | Window closes promptly; queued work is canceled; late callbacks do not update the closed window |
| Resize/reconnect in the manager, then Move | Inline completion; current font size reloaded; Move closes the window and activates the existing single-click tool |
| Select several features at one coincident location | Creation stops before the text dialog with a distinct-location explanation |
| Create after changing map CRS, with an existing old-CRS graphics layer | A new matching-CRS graphics layer receives the new label |
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
| Move a source point, select its label with Graphics > Select, reconnect | Endpoint moves to the feature; label position/text stay fixed; completion notification appears |
| Select a label, Move Label, click a new position | Text moves; leaders stay connected to the original source features |
| Select a label, Resize Label, enter 6–72 points | Text size changes; position, leader endpoints, and line width remain unchanged |
| Delete a source point or repoint the layer | Refresh rejects the change and leaves the graphic intact |
| Undo/Redo creation, reconnect, move, and resize | Each named operation reverses/reapplies the complete change |
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
- `src/MultipleLeaders/CalloutService.cs`: source selection, field lookup, coordinate projection, provenance, creation, reconnection, movement, and resizing.
- `src/MultipleLeaders/CalloutManagerWindow.xaml`: label list, explicit edit targets, progress, and cancelable manager actions.
- `src/MultipleLeaders/PlaceCalloutTool.cs`: map-click placement and movement.
- `src/MultipleLeaders/CalloutOptionsWindow.xaml`: label and style options.
- `tests/MultipleLeaders.Checks/`: standalone checks and their limits.

## References

- [Community request: multiple leaders from a single label](https://community.esri.com/en/discussion/931928/multiple-leaders-from-a-single-label-i-e-label-clustering)
- [CIMTextGraphic members](https://doc.esri.com/en/arcgis-pro/latest/sdk/api-reference/topic5721.html)
- [Leaders property](https://doc.esri.com/en/arcgis-pro/latest/sdk/api-reference/topic5737.html)
- [Esri callout and native leader example](https://community.esri.com/en/discussion/1192786/callout-leader-line)
- [Pro text formatting rules](https://doc.esri.com/en/arcgis-pro/latest/help/mapping/text/text-formatting-tags.html)
