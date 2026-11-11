# Road Builder integration

Inspected revision: [5f4b1ab5d1a5ce4f8d05e4fc89c354f3a1d53485](https://github.com/JadHajjar/RoadBuilder-CSII/commit/5f4b1ab5d1a5ce4f8d05e4fc89c354f3a1d53485), project version 0.8.0. No Road Builder code is modified or forked.

## Confirmed in source

- `RoadBuilder.Domain.Components.Prefabs.RoadBuilderLaneInfo : Game.Prefabs.ComponentBase` has `RoadCategory RequireAll`, `RequireAny`, `RequireNone`, `bool NoDirection`, `bool Median`, thumbnail fields and fluent setup methods. `NoDirection` hides a UI direction selector; it does not implement physical bidirectional traffic.
- `RoadBuilderLaneGroup` has `LaneGroupPrefab? GroupPrefab` and `LaneOptionCombination[]? Combination`.
- `RoadBuilder.Domain.Prefabs.LaneGroupPrefab : PrefabBase` has public `RoadBuilderLaneOption[]? Options`. Its `LinkedSections` collection and built-in ownership flag are internal.
- `RoadBuilderLaneOption` has public `Type`, `Name`, `DefaultValue`, `IgnoreForSimilarDuplicate`, `Options`. Variant combinations use `OptionName` and `Value`. The README specifies a Two-way variant value other than `0`; that option selects a prefab variant, not a conflict-management implementation.
- `RoadBuilderNetSectionsSystem` discovers Created prefab entities, populates public `NetSections`, `NetPieces`, `NetLanes`, `LaneGroups`, links groups and emits `SectionsAdded`. `RefreshCustomContent()` relinks and emits a refresh after setup.
- `RoadBuilderNetSectionsUISystem` lists standalone net sections carrying lane info, hides linked variants and filters categories. No general cross-section validation callback was identified in the inspected discovery/UI files.
- `NetworkConfigExtensionsUtil.FindLanes<TLane>(NetSectionPrefab)` is a public helper that checks only unconditional pieces and does not traverse subsections. It is insufficient to establish zero vehicle lanes in every variant. `CreateAndAddEmptyPieces` removes `NetPieceLanes` from cloned pieces; the generated `RB Empty Section 3` is therefore a candidate non-drivable template, checked again before cloning.

The source evidence is indexed in [RESEARCH.md](RESEARCH.md) and `research-sources.json`. The closed section's registration, catalogue discovery, picker appearance and placement were verified in CS2 1.6.4f1; native lane topology and save/load behavior remain unverified.

## Implemented experiment

After explicit opt-in, `ResearchLaneSystem` waits until `RoadBuilderGenerationDataSystem.RoadGenerationData` and its empty template exist. It rejects templates with any subsection, missing pieces or any `NetPieceLanes` component (including conditional pieces), creates a fresh `NetSectionPrefab` under a unique `TWTL` name, copies only the validated empty-piece array, sets standalone lane metadata and registers it with `PrefabSystem.AddPrefab`. Road Builder owns subsequent discovery and list refresh. It references the empty pieces without mutating them or any existing road. The system runs before `PrefabSystem` in MainLoop so newly created prefab entities can be processed in the same frame.

The section appears by prefab name as **TWTL Research Corridor CLOSED 3m**. It is plain non-drivable surface, not a rendered TWLTL or an actual turning lane. The first 1.6.4f1 live picker test did not list it: Unity JSON emitted errors serializing the original cloned Road Builder component's `RoadCategory` enum. The fresh-prefab fix subsequently passed live catalogue discovery, picker search and placement between opposing car-lane groups in a clean two-mod playset. Road Builder persisted the seven-section road config and widened the road from 24 m to 27 m. Native lane-entity inspection and save/load remain open. The default opt-in is off.

Only plain-road categories are exposed for this closed experiment. This is not a statement that functional TWLTL should exclude compatible trams, asymmetric roads or even lane counts. Core validation explicitly permits asymmetric counts and treats crossable trams as WARNING pending actual tests.

## Smallest possible upstream enhancements

No upstream enhancement is needed for basic custom section listing. Before exposing a drivable TWLTL, ask whether Road Builder can provide:

1. A public per-configuration validator returning Valid/Warning/Invalid plus a reason, called before generation and visible in the existing lane/options UI.
2. A post-generation event giving configuration identity, placed owner entities and the regenerated lane map/revision, plus deletion/world-reset notifications.

These are proposed interfaces, not asserted existing APIs. They would allow pre-generation eligibility and reliable revalidation without patching Road Builder's editor. They do not solve route intent, entry gating or opposing-lane braking. Do not implement a fork until maintainers assess these narrow additions and existing events are examined further.

The next physical experiment needs verified `NetPieceLanes`/lane-generation signatures from real game assemblies, one corridor with two opposite logical lanes, and native overlap/pathfinding observations. Creating such lanes from guessed fields or merely setting `NoDirection` would not satisfy the specification.
