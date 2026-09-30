---
title: "ArcGIS Pro Cartography Add-ins"
slug: "arcgis-pro-cartography-addins"
as_of: "2026-09-29"
year: 2026
project_type: "Personal software project"
category: "Geospatial software engineering"
status: "Prototype"
tagline: "Shared-label editing and live-legend resizing inside ArcGIS Pro."
technologies:
  - "C#"
  - ".NET 10"
  - "WPF / XAML"
  - "ArcGIS Pro SDK 3.7"
  - "Cartographic Information Model (CIM)"
  - "PowerShell"
versions:
  multiple_leaders: "0.1.5"
  legend_scaler: "0.1.3"
automated_checks:
  multiple_leaders: 27
  legend_scaler: 21
  total: 48
latest_ui_native_validation: "Pending"
development_method: "AI-assisted development with user-directed requirements and iterative testing"
source_visibility: "Private"
source_commit: "668acc5cd441e62466730303f9ec8e7a8fed2da0"
repository_url: null
demo_url: null
download_url: null
---

# ArcGIS Pro Cartography Add-ins: portfolio handoff

Use this file to add or update **one portfolio project containing two independent add-ins**. Lead with Multiple Leaders, then present Legend Scaler as the companion tool. The public copy below can be adapted directly to the existing portfolio's content model.

This handoff reflects the current local implementation as of September 29, 2026. It supersedes the earlier `ArcGIS_Pro_Addins_Portfolio_Report.md` and PDF for portfolio implementation. Those reports describe Multiple Leaders 0.1.4 and Legend Scaler 0.1.2; this handoff covers **0.1.5 and 0.1.3**.

## Public copy: project card

**Title:** ArcGIS Pro Cartography Add-ins

**Subtitle:** Shared-label editing and live-legend resizing in C# and .NET.

**Description:** Two independent ArcGIS Pro extensions for focused cartographic tasks: connect one editable label to several point features, and resize supported contents of a live layout legend. Built with C#, .NET, WPF, and the ArcGIS Pro SDK, with native graphics, source-aware reconnection, style previews, and 48 passing local automated checks.

**Status label:** Personal project / Prototype

**Technology labels:** C#, .NET 10, WPF, ArcGIS Pro SDK, CIM

**Card action:** View case study. Use an internal route matching the portfolio's conventions; no public source, live demo, or installer destination is supplied.

## Public copy: case study

### Overview

I built two ArcGIS Pro add-ins to turn specific cartographic editing tasks into guided desktop workflows. Multiple Leaders creates a shared text graphic connected to selected point features and provides controls for managing, moving, resizing, and reconnecting it. Legend Scaler applies a percentage transform to supported properties of a live layout legend.

This is a personal project developed with AI-assisted coding and iterative requirements and testing. It demonstrates geospatial software engineering through native GIS integration, coordinate-system handling, interface design, cancellation, and verification inside a desktop application.

### The problem

A shared description may need to point to several map features. Maintaining the text and its leader endpoints separately makes later edits harder to manage. Layout legends present a related editing problem: changing their overall size can involve text, spacing, symbol patches, the frame, and fitting behavior.

The project packages these tasks into two independent add-ins. Each has its own interface, implementation, checks, build process, and installable package.

### Multiple Leaders: one label, several points

The user selects point features, enters shared text or reads a common string-field value, and clicks a label position. The add-in creates one native text graphic with leader lines to the distinct source locations. Source features and attributes remain unchanged.

The current interface includes:

- **Manage Labels:** choose an existing label by its text, source layer, feature count, and graphics layer. Move, resize, or reconnect it without first selecting a graphic in the map.
- **Move Label:** place the text at a new location while rebuilding leaders to the original source features.
- **Resize Label:** change the text size using a numeric value or preset, with a style sample and current-to-new size feedback.
- **Reconnect Leaders:** explicitly refresh endpoints after source points move while retaining the label's existing text, position, and styling.
- **Guided creation:** a live style sample, font and line-width presets, a character count, and size choices remembered during the ArcGIS Pro session.

The implementation stores source membership and a data-connection fingerprint with the graphic. Later edits validate that saved relationship rather than relying on whichever features happen to be selected at the time.

### Legend Scaler: percentage-based resizing

The user selects a live layout legend, chooses a percentage, and applies it to the original or a new live copy. The transform covers supported font sizes, gaps, symbol patches, item indents, frame dimensions, and selected effects within the legend definition.

The current interface includes:

- Presets for 50%, 75%, 100%, 125%, 150%, and 200%.
- A live explanation of how much larger or smaller the result will be.
- A before-and-after frame preview in the layout's units.
- An explicit choice between a new copy and the selected legend, defaulting to a copy.
- Completion and overflow feedback, with Close available during queued work.

The legend retains its live map connection. Some visible symbol sizes remain controlled by map renderers, so proportional scaling of every legend component is outside the current guarantee.

### Engineering approach

The add-ins use **C# and .NET 10**, **WPF/XAML** interfaces, and the **ArcGIS Pro SDK 3.7**. Service code performs GIS operations through Pro's worker-thread queue. Separate CIM builders and transformations handle native graphics and legend properties.

Native testing exposed behavior that compilation alone could not establish. Shared-label movement required the native element anchor API rather than only changing the text graphic's geometry. Placement cancellation required tool changes to occur after map-tool callbacks completed. These findings shaped the implementation and its regression checks.

The usability revision added earlier validation, preserved captured label identity during editing, and kept unexpected recovery failures visible even after a dialog closes. Build scripts validate the ribbon configuration and package each add-in without redistributing Esri runtime assemblies.

### Results and current status

Both current Release builds pass with zero warnings and errors. The local harnesses pass **48 checks: 27 for Multiple Leaders and 21 for Legend Scaler**. Package contents and DLL versions were checked against the builds. The revised dialogs were also rendered with synthetic values and inspected for basic layout behavior, including reduced-height creation and legend dialogs.

Earlier versions passed selected native tests in ArcGIS Pro 3.7.2: creation of a label with three leaders, reconnecting an endpoint after a source point moved, moving and resizing label text, and resizing a legend frame with Undo. The latest label browser and usability revisions still require native acceptance testing. Production adoption, performance gains, and time savings have not been measured.

### Scope

Multiple Leaders implements manually placed graphics for point features in 2D maps. It does not provide automatic clustering, collision avoidance, or feature-linked annotation. Reconnection is explicit and does not automatically refresh text copied from a field. Coordinate-system restrictions apply.

Legend Scaler supports selected properties of ordinary live legends. Renderer-controlled symbols, complex styles, copy behavior, and broader persistence/export cases require further qualification.

## Implementation mapping for the portfolio project

Keep this section and the evidence appendix as implementation guidance. They do not need to appear on the public page.

| Portfolio element | Content to use |
| --- | --- |
| Project title and route | Title and slug from the metadata; adapt the route to the site's existing convention. |
| Listing card | Public project-card description, prototype label, and concise technology labels. |
| Project detail | Overview, problem, Multiple Leaders, Legend Scaler, engineering approach, results, and scope. |
| Main feature | Multiple Leaders, especially the relationship between one graphic and its original source points. |
| Companion feature | Legend Scaler and its explicit percentage/copy workflow. |
| Evidence callout | 48 local automated checks, with 27/21 breakdown; latest native UI validation pending. |
| Source/demo/download buttons | Omit because no public destinations are supplied. |
| Ownership | Personal project, AI-assisted development, user-directed requirements and iterative testing. |
| Classification | Use the existing GIS development or software engineering category without changing the site's broader grouping. |

### Visual content

No verified public screenshot assets are attached to this handoff. A simple vector diagram can illustrate three points connected to a shared label, labeled **Conceptual workflow**. It must not be presented as an ArcGIS Pro screenshot.

If application screenshots are added later, use synthetic data and remove account details and machine paths. The detached WPF previews from development are layout checks; they are not evidence of the new workflows running inside Pro. Avoid generic stock imagery or invented application screens.

### Copy-ready instruction for the receiving project

```text
Add or update one portfolio project titled "ArcGIS Pro Cartography Add-ins"
using the attached PORTFOLIO_HANDOFF.md.

Work in this project's current working directory. Read its repository
instructions and inspect its existing project content model before editing.
Preserve its visual design, navigation, and project grouping. Reuse the
existing card and detail-page components instead of creating a separate
design system. Avoid duplicating an existing ArcGIS Pro add-ins entry.

Use the handoff's public copy for the card and case study. Lead with Multiple
Leaders, then show Legend Scaler. Include the stack, implementation approach,
prototype status, and evidence-based results. Keep the 48 local automated
checks distinct from native tests on earlier versions. Do not imply the
latest usability changes have been tested inside ArcGIS Pro.

Keep private source and local QA material out of the public site. Do not
invent screenshots, demo links, downloads, users, time savings, performance
figures, or production deployment. Omit buttons without a public destination.
Use synthetic visuals only, labeled accurately.

Run the portfolio project's required validation and check the affected
desktop and mobile layouts. Report the changed files and validation results.
Publish or push only when authorized in that portfolio project's conversation.
```

## Evidence appendix

### Version-specific verification

| Component and version | Evidence | Boundary |
| --- | --- | --- |
| Multiple Leaders 0.1.5 | Release build; 27 standalone checks; DAML validation; package verification; detached WPF layout review. | Manage Labels, revised dialogs, new preflight checks, and target-layer CRS policy have not been exercised natively in this version. |
| Multiple Leaders 0.1.4 | Native reconnect after a point moved; resize from 12 to 18 points; movement to a clicked position; single-step move/resize Undo and Redo; pending-move Escape. | Selected synthetic examples; not complete projection, rollback, or persistence coverage. |
| Multiple Leaders 0.1.3 | Native creation of one label with three leaders; selected cancellation/exit paths; shared-field lookup; graphics-layer visibility. | Creation exposed separate Undo entries; single-step creation Undo is not established. |
| Legend Scaler 0.1.3 | Release build; 21 standalone checks; DAML validation; package verification; detached WPF layout review. | New presets, copy/original UI, notifications, and keyboard behavior require native acceptance testing. |
| Legend Scaler 0.1.2 | Native 125% frame scaling from 5.5 x 4.25 to displayed 6.875 x 5.313 layout units; Ctrl+Z restored the original frame; idle dialog exit. | These results do not prove proportional rendering of every legend component. |

Local validation used ArcGIS Pro 3.7.2 assemblies and .NET SDK 10.0.204 on Windows. The automated harnesses do not establish native rendering or SDK interaction behavior. Reconnect Undo, full metadata restoration, broad save/reopen behavior, and exported-map PDF behavior remain incompletely qualified.

### Source traceability

The implementation described here is recorded in commit `668acc5cd441e62466730303f9ec8e7a8fed2da0` (Multiple Leaders 0.1.5 and Legend Scaler 0.1.3). The earlier base commit `202ff3ed60454f0359604061cd40786ca67a8f7c` belongs to the historical report and does not contain this usability revision. Portfolio documents are recorded in a separate subsequent commit.

The following paths are relative to the add-ins repository, provided for evidence review rather than as public links:

| Evidence | Repository location |
| --- | --- |
| Versions, builds, checks, package hashes, native results, and remaining coverage | `VALIDATION.md` |
| Multiple Leaders workflow and scope | `MultipleLeaders/README.md` |
| Label manager and edit routing | `MultipleLeaders/src/MultipleLeaders/CalloutManagerWindow.xaml.cs`; `EditCalloutButtons.cs` in the same folder |
| Source identity, selection, coordinate handling, and mutations | `MultipleLeaders/src/MultipleLeaders/CalloutService.cs` |
| Graphic construction and input validation | `MultipleLeaders/src/MultipleLeaders/CalloutGraphicBuilder.cs`; `CalloutInput.cs` in the same folder |
| Placement and tool-exit lifecycle | `MultipleLeaders/src/MultipleLeaders/PlacementLifetime.cs`; `PlacementExitScheduler.cs` in the same folder |
| Legend workflow and limits | `LegendScaler/README.md` |
| Legend scaling, mutation, and input handling | `LegendScaler/src/LegendScaler/LegendScaling.cs`; `LegendScaleService.cs`; `LegendScaleInput.cs` in the same folder |
| Regression harnesses | `MultipleLeaders/tests/MultipleLeaders.Checks/`; `LegendScaler/tests/LegendScaler.Checks/` |
| Build and installer creation | Each add-in's `build.ps1` |

### Publication boundaries

- Use the concrete capabilities and observed results above. Do not convert intended convenience into measured productivity gains.
- Keep the prototype status and latest native-validation boundary visible in the case study.
- Keep private repository URLs, local user paths, QA projects, cache backups, and application logs out of the public page.
- Do not claim an automatic labeling engine, a Maplex extension, a new rendering algorithm, universal legend scaling, marketplace certification, or production deployment.
- Preserve the distinction between implemented behavior, standalone checks, detached UI rendering, and version-specific native tests.
