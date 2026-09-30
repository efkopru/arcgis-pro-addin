# Add-in validation

Date: September 29, 2026. Local runtime: ArcGIS Pro 3.7.2, .NET SDK 10.0.204.

Current packages are Legend Scaler **0.1.3** and Multiple Leaders **0.1.5**. Each add-in has its own source, build script, checks, and generated installer folder. Native results below remain attributed to the versions actually tested.

## Automated checks

- Both Release builds compile with zero warnings and zero errors.
- Legend Scaler: **21 checks** pass, including the production cancellation/restore wrapper with simulated mutations and localized percentage input/change descriptions.
- Multiple Leaders: **27 checks** pass, including single-placement lifetime, cancellation before queued work, deferred tool exit, reentrant cancellation, stale requests, other-tool selection, recovery after a failed transition, localized size input, and shared text validation before placement.
- Both DAML files pass the installed Pro schema. Packages contain their own add-in binaries and Config.daml, without redistributed Esri assemblies.

Current package manifests and DLL versions were checked after packaging. Both archives contain four intended entries, and their DLL hashes match the final Release build outputs. Previous installers are retained in each add-in's ignored `artifacts/archive/` folder.

- Legend Scaler 0.1.3 package SHA-256: `472E1BE3A830F7ACC7FE7B17D26488C8ED1A14E92FA0E492301B7DDCA7BACCE9`.
- Multiple Leaders 0.1.5 package SHA-256: `021D09C1C86378584F581A10CD346A7D2336DD4E6330C3F4CBFE4E42B4E3FBB3`.

## Usability revision: Legend Scaler 0.1.3 and Multiple Leaders 0.1.5

Legend Scaler adds common percentage presets, localized percentage input with an optional percent sign, live larger/smaller feedback, frame dimensions in layout units, an explicit copy/original choice, and persistent completion or overflow notifications. Close remains accessible while the content scrolls or an edit is queued.

Multiple Leaders adds a label browser showing text, source layer, feature count, and graphics layer. It supports movement, resizing, and reconnection without first selecting a graphic. Direct ribbon commands use a single editable selection or open the browser. The creation and resize dialogs add style samples and size presets; creation remembers accepted sizes for the Pro session. Invalid text, coincident-only source selections, and hidden target graphics layers fail earlier. New labels use a target layer matching the map coordinate system so the current Move restriction does not immediately prevent editing a newly created label.

Code review checked captured-element identity, stale source metadata, child versus parent cancellation during a manager-to-Move handoff, and reporting of unexpected recovery errors after closing a dialog. This is source review, not proof of native rollback.

The creation, resize, manager, and legend XAML layouts were rendered as detached WPF content with synthetic values. The rendered layouts were inspected; the creation and legend dialogs were also checked at reduced heights with their action/close footers visible. These checks establish basic layout behavior, not Pro theme integration or SDK event behavior. The mock renders remain in ignored `.tools/ui-preview/`.

No new native tests or add-in installation were performed for these revisions. Before extending the native verification claims, test:

- Manage Labels with no selection, several labels, a selected valid label alongside locked/grouped entries, missing sources, hidden layers, and long text; verify the intended row is edited.
- Resize and reconnect repeatedly inside the manager; close during queued work; confirm expected cancellation is quiet and unexpected restoration failures remain visible.
- Move from the manager, cancel with Escape, then move again; verify endpoints and single-step Undo/Redo remain correct.
- Create after changing the map coordinate system; verify the new target layer and Move workflow, while preserving older graphics.
- Style presets, shared-field loading, manually edited field text, and comma-decimal input; verify previews are labeled as samples and the produced CIM sizes match the inputs.
- Legend presets, copy/original choice, real layout units, completion/overflow notices, and Close/Escape during edits; retest copy creation and Undo/Redo.

## Native setup

Tests use an isolated `Addin QA.aprx` project, three invented point features with a shared string value, and a layout containing `Test Legend`. The project, cache backups, and synthetic data remain in the ignored `.tools/native-qa/` directory. No user working project was edited.

An earlier attempt loaded stale 0.1.0 cached DLLs despite updated packages. Renaming only these two add-ins' cache directories into the local QA backup allowed Pro to extract the current packages. Installed packages and regenerated DLLs were checked by assembly version and SHA-256 hash. Ribbon captions alone were not used as proof of loaded code.

## Legend Scaler 0.1.2 native results

- Close, Esc, and title-bar X each closed the idle dialog.
- Scale original at 125% completed and closed the dialog. Reopening reported a frame change from 5.5 x 4.25 to 6.875 x 5.313 layout units; displayed values are rounded.
- Ctrl+Z restored the frame to 5.5 x 4.25, verified by reopening the dialog.

These checks establish the tested frame resize and Undo, not proportional rendering of every legend component.

## Multiple Leaders native findings

Version 0.1.2 passed options-dialog Cancel and Esc, shared-field lookup, and entry into placement. Esc and Cancel Placement then failed to leave placement mode. No graphic was created during those cancellation tests.

Version 0.1.3 removes awaited tool changes from MapTool callbacks. It cancels the request immediately and schedules one tool transition after callbacks finish. A scoped WPF input filter handles Esc when the sketch-tip popup owns keyboard focus.

Native retesting with the regenerated 0.1.3.0 DLL passed:

- Esc before the first click while the sketch-tip popup had focus.
- Cancel Placement, followed by another placement request. Both cancellation paths left no graphic and restored idle ribbon controls.
- Switching from placement to Pro's Explore tool removed the placement tip, restored idle controls, and left the existing test graphic unchanged without adding the canceled label.
- Shared-field lookup returned Buildings. One map click created one visible Buildings label with three leaders terminating at the selected points, then exited placement.
- Clearing the graphics-layer visibility checkbox hid the label and leaders.
- Named Undo/Redo history entries removed and restored the graphic. Pro exposed separate map and `( Elements )` creation entries, so a single-step creation Undo is not established. Visibility changes also have their own entries; the empty graphics layer remained after reversing the graphic.

Multiple Leaders package SHA-256: `1976F57C14E699B794D8ED58D85171FD6D3BD71F58A05627FD76B0DE674E9E35`.
Regenerated 0.1.3.0 DLL SHA-256: `EAE96DD37E74E8EF6F50268B966381972F34D38AC80C849384FC48F029F7AD37`.

## Multiple Leaders 0.1.4 native results

Version 0.1.4 adds Move Label, Resize Label, separate graphic-selection errors, and a completion notification for Reconnect Leaders. It resolves missing graphic spatial references from SDK geometry and the owning graphics layer. Missing per-point spatial references were a code-review concern, not an established cause of the reported failure.

The 0.1.4 Release build passes with zero warnings/errors and all 21 existing standalone checks. The installed package matches the generated package. The extracted DLL is version 0.1.4.0 and matches the packaged DLL by SHA-256.

Final 0.1.4 package SHA-256: `AB96805D2C058CE6A2C76148B38125ECC50345240960C03B2B096B0FD75AB5E7`.
Final 0.1.4.0 DLL SHA-256: `AE19A04906CB6136943C92E213711193E6623F8B469F5CB9E8C82574FF582C17`.

A backup preserves the synthetic project before changing source ObjectID 3 from (50, 80) to (75, 100) in EPSG:3857. Reopening initially showed the leader still ending at the old location.

- Reconnect Leaders moved that endpoint onto the changed source point while retaining the existing label position and text.
- Resize Label changed the text from 12 to 18 points without moving the source endpoints. One Ctrl+Z restored the prior size and Ctrl+Y reapplied it. Saved CIM retained the edited text, 18-point size, 2-point leader width, and correct endpoints at (0, 0), (100, 0), and (75, 100).
- The initial Move implementation updated CIM Shape but did not relocate native point text. The repaired implementation uses SetAnchorPoint, rebuilds leaders from source IDs, and checks the resulting position. Native retesting moved the text to the clicked location while retaining all three source endpoints and the 18-point font, then returned to idle.
- One Ctrl+Z restored the pre-move label position with its style and endpoints intact. Ctrl+Y reapplied the move.
- Escape canceled a pending Move request, returned the ribbon to idle, and left the label unchanged.

Move is limited to matching map and graphics-layer coordinate systems because native anchor placement uses bare XY coordinates. Resize and Reconnect do not add this restriction. Tests did not establish the missing-SR fallback or interruption during native mutation.

## Remaining native coverage

- Legend copy creation; cancellation during queued or running native edits; full symbol/font/fitting/anchor restoration and Redo; richer legend styles.
- Multiple Leaders cancellation during queued creation or field lookup, title-bar X, rapid duplicate clicks in Pro, coincident points, projection differences, literal special characters, grouped graphics, and complete Undo/Redo metadata restoration.
- Broader project persistence and PDF export for both add-ins. Multiple Leaders reopened and reconnected the tested existing label; that does not qualify every style and projection.

The detailed acceptance tables remain in each add-in's README. Automated checks do not establish native rendering, persistence, or worker-queue cancellation behavior.
