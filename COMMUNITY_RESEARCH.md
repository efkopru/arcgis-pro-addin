**ArcGIS Pro add-in ideas grounded in user requests**

Selected implementation after the later active-ideas review: **[Multiple Leaders](MultipleLeaders/README.md)**. The earlier Legend Scaler prototype is retained. See [README.md](README.md). The shortlist below records an earlier research pass.

Research date: September 28, 2026. This brief revises the earlier brainstorming. The user already knows C# and .NET; the decision is which recurring workflow deserves a product.

The earlier technical search established SDK feasibility but did not validate the idea rankings against user requests. This pass searched Esri Community Ideas and questions, GIS Stack Exchange, and Reddit, then checked current Esri documentation and relevant vendor capabilities. The useful evidence was concentrated in Esri Community. No users were contacted, no posts were made, and no market-size or willingness-to-pay study was conducted.

**Revised shortlist**

| Rank | Concept | Evidence of need | Proposed first product slice | Main limitation |
| --- | --- | --- | --- | --- |
| 1 | Batch Cartography Editor | Requests to change symbol/label properties across layers and a 2024 request for centralized organizational colors | Select several layers, change a small supported set of label properties, inspect proposed changes, apply, and restore prior definitions | Renderer/label-class compatibility; existing batch tools and scripts already cover some operations |
| 2 | Project Source Inspector | 2023 and 2025 requests to distinguish portal/environment origins; the latter describes DEV/TEST/PROD services with the same names | Inventory layers across project maps, show endpoints, assign environment labels, flag mixed environments, navigate to affected layers | Environment naming is organization-specific; a local scan cannot prove recipient access or all external dependencies |
| 3 | Selection Workspace | Requests for previous-selection recovery and a December 2024 request to add features to an existing selection layer | Recent selection history, restore, named sets, add/remove membership, and union/intersection | Existing selections and selection layers already exist; persisted OIDs can become invalid |
| 4 | Dataset Change Explorer | 2024 valve-data comparison report and longer-running complaints about additions/deletions disrupting comparison alignment | Compare two independent point-feature deliveries by stable ID, inspect additions/removals/movement, save review decisions | Native version review and FME already cover substantial change-detection functionality |

This ranking is a product judgment informed by observed requests and scope. It does not mean the listed ideas represent the most popular requests across the whole ArcGIS market.

**1. Batch Cartography Editor**

[Change symbol or label properties for multiple layers](https://community.esri.com/t5/arcgis-pro-ideas/change-symbol-or-label-properties-for-multiple/idi-p/926431) originated November 15, 2010 and remains Under Consideration on the inspected page. Its ArcMap origins limit how strongly it can establish present ArcGIS Pro demand.

The newer [centralized color-theme request](https://community.esri.com/t5/arcgis-pro-ideas/implement-a-centralized-color-theme-system-with/idi-p/1420602), May 8, 2024, is Open and describes maintaining organization colors across multiple layers.

Current [Esri property-copy documentation](https://doc.esri.com/en/arcgis-pro/latest/help/mapping/layer-properties/copy-and-paste-properties-between-layers-and-tables.html) says properties cannot be pasted to more than one layer or table at a time. Batch Apply Symbology and scripting are alternatives. The proposed value is selective, compatible changes across multiple layers, preserving unrelated settings and making results easy to review and restore.

First prototype: label font family and size across selected layers and label classes. Read the relevant definitions, calculate proposed changes, display a compatibility/preview list, apply only supported changes, and retain restorable prior definitions. Add halo styling, symbol colors, and line widths only after the first property operations work reliably. Restoring definitions is a product requirement to verify in the SDK, not a claim that every property mutation participates in native edit undo.

**2. Project Source Inspector**

[List by Data Sources does not distinguish Enterprise portals](https://community.esri.com/t5/arcgis-pro-ideas/list-by-data-sources-pane-doesn-t-display/idi-p/1603796), April 8, 2025, is Open. The user describes same-named services across PROD/TEST/DEV and difficulty identifying mixed environments.

[Add portal source name in layer properties](https://community.esri.com/t5/arcgis-pro-ideas/add-portal-source-name-in-the-layer-properties/idi-p/1276969), April 10, 2023, is also Open.

First prototype: scan the current project; display map, layer, service endpoint or database, configured environment label, and connection state. Filter mixed-environment maps and jump to an affected layer. Keep the initial feature read-only. A second release could show which layouts use the affected maps.

Native source properties and connection-management tools already exist. [Broken-link documentation](https://doc.esri.com/en/arcgis-pro/latest/help/mapping/layer-properties/repair-broken-data-links.html) limits repair of web-layer sources; do not promise universal automatic repair. Read-only visibility is the first value proposition.

**3. Selection Workspace**

[Add features to Selection Layer](https://community.esri.com/t5/arcgis-pro-ideas/add-features-to-selection-layer/idi-p/1564846), December 4, 2024, is Open. The request concerns updating existing membership rather than reconstructing the layer.

[Implement a Select Previous command](https://community.esri.com/en/discussion/936978/implement-a-select-previous-command) is also Open and includes accounts of losing large manual selections. A reliable original date was not available on the migrated page.

First prototype: automatically retain recent per-layer selection states within a session, restore a chosen state, and create named snapshots. Then add set operations and explicit membership editing. Avoid replay loops when restoration itself triggers selection events.

Official [selection-layer documentation](https://doc.esri.com/en/arcgis-pro/latest/help/mapping/layer-properties/selection-layers.html) warns about ObjectID/FID references when source data changes. Durable cross-session sets should require validated stable IDs or clearly expire; a session-only prototype avoids promising durability it cannot deliver.

A basic recent-query list is weaker: the [related request](https://community.esri.com/t5/arcgis-pro-ideas/table-select-by-attributes-list-most-recent/idi-p/1075122) is marked Already Offered, with Esri describing history-based support in Pro 3.0.

**4. Dataset Change Explorer**

A [June 17, 2024 valve-data discussion](https://community.esri.com/en/discussion/1494049/how-can-we-compare-geometry-between-two-fcs-to-see-what-is-different-like-two-version-of-a-point-layer) describes a daily comparison intended to find moved, added, and deleted valves that reported thousands of apparently unchanged records. A subsequent comment requests review of changes one at a time. This is a concrete user report, not a universal product diagnosis.

The [Feature Compare Tool Upgrade idea](https://community.esri.com/en/discussion/961628/feature-compare-tool-upgrade) is marked Implemented as of April 12, 2023 because sort-field selection exists. Esri explicitly distinguishes the separate added/removed-record issue in that resolution. Current [Feature Compare documentation](https://doc.esri.com/en/arcgis-pro/latest/tool-reference/data-management/feature-compare.html) still documents the sequencing limitation after additions/deletions.

Important competing capabilities:

- [Native branch Version Changes](https://doc.esri.com/en/arcgis-pro/latest/help/data/geodatabases/overview/version-changes-branch.html) already displays inserted, deleted, and updated records, changed attributes, and geometry comparisons.
- [FME ChangeDetector](https://docs.safe.com/fme/html/FME-Form-Documentation/FME-Transformers/Transformers/changedetector.htm) provides key-based matching, change outputs, geometry tolerance, and attribute difference information.

The remaining hypothesis is convenient review of independent deliveries directly in Pro with saved decisions. It is not a new change-detection algorithm or a replacement for enterprise version management.

**Ideas demoted or excluded after checking existing capabilities**

- Generic Feature Dossier: [native related-record pop-ups](https://doc.esri.com/en/arcgis-pro/latest/help/mapping/navigation/use-related-data-in-pop-ups.html), [cross-table Arcade lookup](https://support.esri.com/en-us/knowledge-base/display-fields-from-a-related-table-in-the-pop-up-windo-000035545), and [XTools Identify Pro](https://xtools.pro/en/features/feature-tools/identify-pro/) create substantial overlap. A domain-specific report may still have value, but the generic pane was not a well-supported first recommendation.
- Filename-to-asset attachment matching: [Generate Attachment Match Table](https://doc.esri.com/en/arcgis-pro/latest/tool-reference/data-management/generate-attachment-match-table.html) already supports exact, prefix, suffix, and anywhere matching. A stronger product would need ambiguity handling or multiple evidence types.
- Attachment pagination in reports: the [user request](https://community.esri.com/en/discussion/1516026/repeat-attachment-frame-in-reports-to-show-all-attachments) is Implemented, updated May 19, 2026 for Pro 3.7. Older complaints no longer establish this as a current gap.
- Basic batch layout export: [Export Layouts](https://doc.esri.com/en/arcgis-pro/latest/tool-reference/data-management/export-layouts.html) is present in Pro 3.7.
- Schema comparison alone: current native schema tools already cover this. Dependency inspection remains a separate hypothesis because [schema-report documentation](https://doc.esri.com/en/arcgis-pro/latest/help/data/geodatabases/overview/schema-report.html) explicitly describes dependency-identification limits.
- Bulk Edit Preview: this search did not establish enough direct demand to keep it in the top tier. It remains an unvalidated concept, not a disproved one.

A useful additional direction is an interactive dependency index. [Layer Use Index](https://community.esri.com/en/discussion/1698765/layer-use-index), currently Open with an April 2026 update in the inspected listing, requests finding which maps use a data source. An existing shared Python inventory tool is a competing workaround; an add-in would need interactive navigation and clear coverage to add value.

**Decision from this research**

Batch Cartography Editor is the strongest first candidate for broad everyday usefulness. Project Source Inspector addresses the more specific enterprise failure mode. Selection Workspace is the smallest prototype. Dataset Change Explorer has concrete demand but stronger existing competition and more comparison-semantics work.

Before committing beyond a prototype, test the proposed workflow with users who perform the relevant task, compare against their actual native-tool/script workflow, and determine whether the improvement justifies adoption. Open status, comments, and votes establish interest; they do not establish buying intent.

Search result files and directly inspected discussion snapshots are preserved locally under .firecrawl/, which is excluded from this repository. The source links above remain available to repository readers. Two additional Firecrawl queries returned INVALID_ARGUMENT and were not treated as evidence; alternate searches and direct page reads supplied the findings above.
