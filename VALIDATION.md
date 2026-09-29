# Version 0.1.2 validation

Date: September 28, 2026. Local runtime: ArcGIS Pro 3.7.2, .NET SDK 10.0.204.

The add-ins now live in separate `LegendScaler/` and `MultipleLeaders/` folders. Their build scripts and generated installers are inside those folders. Folder restructuring does not change the native test limitations recorded below.

## Completed

- Both Release builds compile with zero warnings and zero errors.
- Legend Scaler: 17 checks pass. This includes the production cancellation/restore wrapper with simulated mutations.
- Multiple Leaders: 15 checks pass. This includes the production placement lifetime, cancellation before a click, concurrent clicks, and canceled queued work.
- Both DAML files pass the installed Pro schema.
- Both packages contain their expected Config.daml and add-in binaries, with no redistributed Esri assemblies. Extracted DLL assembly versions are 0.1.2.0.
- An isolated ArcPy project was created with three invented point features and a layout legend. No edits were made to the user's working project.

## Native test limitation

The isolated project opened in Pro and the new ribbon captions appeared. However, both cached DLLs were still assembly version 0.1.0.0. Copying the 0.1.2 DLLs into these add-ins' cache directories failed with the explicit error that the files were in use by another process. Commands in the test instance remained disabled.

The new package files were placed in the existing per-user add-in locations. Previous packages and cache files were backed up locally under the ignored `.tools/native-qa/` directory. All Pro instances must close before the updated code can load. **This session did not verify 0.1.2 dialog closing, Escape dispatch, creation, or Undo inside Pro.**

## Required next native checks

1. Close all Pro instances, install the rebuilt packages, and reopen an isolated test project. Confirm the loaded add-in DLL version is 0.1.2.0.
2. Legend Scaler: open Resize Legend with one legend selected. Verify Close, Esc, and title-bar X. Repeat while an apply waits in the worker queue. Confirm canceled work makes no delayed edit.
3. Multiple Leaders: select three points, open Create Shared Label, and verify Cancel, Esc, and title-bar X before placement, including during field lookup.
4. Enter text, choose Next: place on map, and cancel using Esc, Cancel Placement, and a different map tool. No new graphic should appear.
5. Create one graphic, then confirm a second click does not create another. Hide the Multiple Leaders graphics layer and confirm the output disappears.
6. Verify copy/original legend results, leader endpoints, Reconnect Leaders, Undo/Redo, save/reopen, and PDF export using the acceptance tables in the [Legend Scaler README](LegendScaler/README.md) and [Multiple Leaders README](MultipleLeaders/README.md).
