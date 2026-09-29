# Add-in validation

Date: September 28, 2026. Local runtime: ArcGIS Pro 3.7.2, .NET SDK 10.0.204.

Legend Scaler is version **0.1.2**. Multiple Leaders is version **0.1.3**. Each add-in has its own source, build script, checks, and generated installer folder.

## Automated checks

- Both Release builds compile with zero warnings and zero errors.
- Legend Scaler: **17 checks** pass, including the production cancellation/restore wrapper with simulated mutations.
- Multiple Leaders: **21 checks** pass, including single-placement lifetime, cancellation before queued work, deferred tool exit, reentrant cancellation, stale requests, other-tool selection, and recovery after a failed transition.
- Both DAML files pass the installed Pro schema. Packages contain their own add-in binaries and Config.daml, without redistributed Esri assemblies.

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

## Remaining native coverage

- Legend copy creation; cancellation during queued or running native edits; full symbol/font/fitting/anchor restoration and Redo; richer legend styles.
- Multiple Leaders cancellation during queued creation or field lookup, title-bar X, rapid duplicate clicks in Pro, coincident points, projection differences, literal special characters, positive Reconnect Leaders, moved/grouped graphics, and complete Undo/Redo metadata restoration.
- Project save/reopen and PDF export for both add-ins.

The detailed acceptance tables remain in each add-in's README. Automated checks do not establish native rendering, persistence, or worker-queue cancellation behavior.
