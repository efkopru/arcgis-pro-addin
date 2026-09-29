**ArcGIS Pro add-in concepts and implementation plan**

Selected implementation: **[Multiple Leaders](MultipleLeaders/README.md)**. The earlier Legend Scaler prototype is retained. See [README.md](README.md) for the repository overview. The concepts below are historical alternatives.

Research update: the original idea rankings below are superseded by [the community research brief](COMMUNITY_RESEARCH.md). The restricted dataset-reviewer implementation remains one candidate, not the selected product.

Prepared September 28, 2026. This was the initial product exploration and engineering plan, before the Legend Scaler prototype. Rankings are judgments about a manageable first product, not verified market demand or proof of novelty.

C# with the ArcGIS Pro SDK for .NET fits an add-in whose value depends on map selection, layer access, dockpanes, and interactive review. A workflow consisting only of batch processing can first be proved with a Python toolbox. Use the add-in when the interaction itself saves work.

| Priority | Concept and initial user | Smallest useful release | Implementation approach | Main uncertainty |
| --- | --- | --- | --- | --- |
| 1 | Dataset Update Reviewer for analysts receiving recurring asset deliveries | Compare two point feature classes by stable asset ID; classify additions, removals, attribute changes, and point movement; review on the map; save decisions and export a report | C# comparison library, Pro data adapter, WPF dockpane, local review store | Reliable IDs and enough review friction to justify a dedicated tool |
| 2 | Project Handoff Preflight for GIS consultants and team leads | Inspect the open project's layers for broken sources and selected delivery requirements; export a dependency manifest with unresolved items | Project/layer inspection adapter plus configurable rules and report writer | A local scan cannot establish recipient permissions or discover every external dependency |
| 3 | Evidence Review Desk for municipal asset analysts | Link a selected asset to dated photos/documents; mark missing or stale evidence; record a decision and export an evidence index | Feature selection integration, explicit asset-ID association table, evidence viewer, provenance fields | Source access, capture-date quality, and defensible feature-to-evidence matching |
| 4 | Vendor Delivery Intake Assistant for analysts normalizing repeated submissions | Compare source fields against a saved schema, preview field/value mappings, flag invalid conversions, export a new staged dataset and rejection table | Mapping profiles plus existing geoprocessing where suitable; explicit conversion rules | Vendor-specific exceptions can expand scope quickly; generic ETL is a crowded category |
| 5 | Parcel-Building Reconciliation Desk for property GIS teams | Flag ambiguous building-to-parcel relationships; inspect overlapping sources and their dates; classify exceptions | Spatial candidate generation followed by map-linked human review | Legitimate multi-parcel buildings and positional error make automatic conclusions unreliable |

Relative effort: handoff preflight is the smallest; a point-only update reviewer is medium; evidence and intake workflows are medium with integration risk; parcel-building review is medium to high. Each estimate increases with enterprise services, additional geometry types, or organization-wide deployment.

The strongest first concept is **Dataset Update Reviewer**, with the positioning: **Review every data delivery before it updates your GIS.** Its value proposition is a repeatable delivery-review process with clear change classification and recorded decisions.

Esri already supplies Feature Compare and Data Reviewer. Current Feature Compare documentation states that additions or deletions can put subsequent comparisons out of sequence. Data Reviewer already handles validation and error management. The proposed opportunity is a narrowly designed delivery-review workflow, including stable-ID reconciliation and review records. This is not evidence that no competing add-in exists. Validate the complete workflow against existing tools before committing to a commercial build.

**First-release user workflow**

1. Choose baseline and incoming point feature classes from local file geodatabases.
2. Select a stable business identifier such as AssetID. ObjectID is not a cross-delivery identity guarantee. Use GlobalID only when the data pipeline preserves it.
3. Validate key uniqueness, required fields, geometry type, spatial reference, and dataset scope. Block ambiguous matching; display duplicate and missing IDs separately.
4. Choose comparison fields, excluded volatile fields, numeric tolerances, and a point-movement threshold with explicit units.
5. Compare immutable input snapshots. Report new, missing, unchanged, attribute-changed, geometry-changed, and both-changed records.
6. Select a result to view the old/new locations and attribute values. Track decisions as unreviewed, accepted, or needs investigation, with a note.
7. Export a CSV change list and an HTML review report. Persist the review for reopening. An incomplete review must remain visibly incomplete.

A feature absent from the incoming dataset is a candidate removal, not proof that the physical asset was removed. Validate filters and delivery coverage before assigning that interpretation. An accepted review decision records the analyst's judgment; it does not apply an edit.

For the first release, use point features with the same declared projected coordinate system and explicit distance units. Reject unsupported geometry types, mixed spatial references, Z/M-dependent comparisons, and nonunique keys with useful diagnostics. Add lines and polygons later, after defining their equality and tolerance semantics.

**Implementation structure**

| Component | Responsibility |
| --- | --- |
| AddIn | Ribbon command, WPF dockpane, view models, progress, cancellation, and map navigation |
| ProAdapter | Read selected feature classes, inspect schema, create snapshots, and select/highlight features |
| Core | Stable-key reconciliation, normalized attribute comparisons, change classification, and rule evaluation |
| Persistence | Versioned JSON profiles and a local SQLite store for runs, changes, and decisions |
| Reporting | CSV and HTML exports with settings, counts, limitations, and review status |
| Core.Tests | Deterministic fixtures for matching, comparison semantics, and review-state integrity |

Keep ordinary comparison logic independent of ArcGIS types. Pass plain records from the adapter into the core. For point geometry, compare coordinates under the documented spatial-reference and units contract; do not develop a general geometry engine.

Use the ArcGIS SDK's required threading model for SDK calls. Dispatch methods requiring the Main CIM Thread through QueuedTask.Run; update WPF controls on the UI thread. Use ordinary background work only for suitable detached computations and file operations. Dispose geodatabase resources promptly and check cancellation between bounded units of work.

For large inputs, stream snapshots to an indexed local store instead of retaining every feature and attribute in memory. Start with a documented dataset-size envelope and measure before expanding it. Distinguish feature counts from individual changed-field counts.

Store a run ID, input fingerprints, stable keys, comparison settings, application version, creation time, per-record changes, and reviewer decisions. Fingerprints should describe the extracted comparison inputs, not imply a complete forensic hash of every geodatabase file. Mark a review stale when its inputs or settings change. CSV export must handle spreadsheet-formula prefixes safely; HTML must escape values.

**Toolchain**

Esri's current ArcGIS Pro 3.7 SDK requirements specify Windows 11, .NET 10, and Visual Studio 2026 version 18.4.1 or higher. The supported stack includes WPF and MVVM. Select the target Pro release used by pilot users before installing tooling; earlier Pro releases have different .NET requirements. No local ArcGIS installation or developer toolchain was verified during this planning task.

Source: [Esri SDK requirements](https://github.com/Esri/arcgis-pro-sdk#requirements).

**Delivery plan**

Planning envelope: approximately 6 to 8 weeks at 15 to 20 focused hours per week for the restricted pilot. This is an estimate, not a deadline. New C#/WPF learning, external pilot availability, and IT deployment requirements can extend it.

| Stage | Deliverable | Exit condition |
| --- | --- | --- |
| 1. Validate the problem | Observe 3 to 5 analysts reviewing comparable deliveries; record their current steps, tools, and review time; define the first asset type | At least two prospective users have a recurring problem and commit to testing a second delivery |
| 2. Prove the comparison | Synthetic baseline/candidate fixtures, stable-ID matching, attribute changes, and point movement | Every seeded change and ambiguity is classified correctly; row order and changed ObjectIDs do not alter matching |
| 3. Integrate into Pro | Ribbon entry, dockpane, layer selectors, result list, old/new inspection, progress, cancellation | A reviewer can investigate every result category directly in the map without UI hangs |
| 4. Preserve decisions | Profiles, persisted run state, notes, reports, and stale-input detection | Close/reopen preserves decisions; exports reconcile with the visible run; incomplete runs remain incomplete |
| 5. Pilot and harden | Measured trials on realistic permitted data, install/uninstall checks, compatibility statement, help text | Users complete a second delivery independently; no missed seeded changes; measured usability and performance issues are resolved |
| 6. Prepare promotion | Synthetic demo data, short video, product page copy, sample report, and documented limitations | Every public claim is supported by a demonstrated feature or measured pilot result |

Meaningful test cases include duplicate/null IDs, records reordered between deliveries, changed ObjectIDs, additions/removals, null versus empty text, numeric tolerance boundaries, dates, coded values, moved points, incompatible schema/CRS, cancellation, stale inputs, reopen behavior, and report escaping. Integration tests require a supported ArcGIS Pro environment; ordinary Core tests can run separately.

Use public or synthetic data for demonstrations. Keep employer datasets and private documents out of the public package.

**Promotion plan**

Lead with the recurring delivery problem. Demonstrate a synthetic asset refresh containing an added asset, an omitted asset, a changed attribute, a moved point, and a duplicate ID. Show detection, map inspection, a recorded decision, and the exported review report in roughly 90 seconds.

Initial audience: municipal GIS analysts, utilities GIS teams, and consultants responsible for recurring asset updates. Candidate channels are a portfolio case study, a focused LinkedIn demonstration, an Esri Community technical post, and a local GIS user-group presentation. This document proposes those activities; nothing has been posted or sent.

Measure review time against the user's current workflow on equivalent deliveries, detection accuracy on seeded cases, and whether the user returns for the next delivery. Do not advertise percentage savings before measuring them.

A possible commercial model is an individual reviewer license with team profiles and deployment support as later offerings. Willingness to pay, budget ownership, procurement friction, and support burden remain unvalidated. A reusable public demo and case study can also make the project useful as a portfolio artifact.

**Verified reference material**

- [Esri SDK overview and requirements](https://github.com/Esri/arcgis-pro-sdk): target runtime, IDE, and supported extension patterns.
- [Feature Compare](https://doc.esri.com/en/arcgis-pro/latest/tool-reference/data-management/feature-compare.html): existing comparison capabilities and record-alignment limitation.
- [Data Reviewer FAQ](https://doc.esri.com/en/arcgis-pro/latest/help/data/validating-data/arcgis-data-reviewer-frequently-asked-questions.html): existing validation and error-management capabilities.
- [SDK framework threading guidance](https://github.com/Esri/arcgis-pro-sdk/wiki/ProConcepts-Framework#using-queuedtask): SDK scheduling constraints.

These sources establish technical feasibility and native overlap. They do not establish market size, a complete competitor landscape, demand, or commercial success.
