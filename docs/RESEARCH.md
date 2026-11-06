# Research ledger

Inspected on 2026-11-06 using the connected GitHub source tools. Local git clones and SDK download failed because the configured cloud proxy refused connections; no proxy bypass was attempted. `research-sources.json` records immutable upstream commits and inspected file blobs. Third-party descriptions and code comments are evidence of those projects' intentions, not authoritative game semantics.

## Road Builder

[5f4b1ab5d1a5ce4f8d05e4fc89c354f3a1d53485](https://github.com/JadHajjar/RoadBuilder-CSII/tree/5f4b1ab5d1a5ce4f8d05e4fc89c354f3a1d53485), MIT, project version 0.8.0.

Read README custom lane instructions; `RoadBuilder/Domain/Components/Prefabs/{RoadBuilderLaneInfo,RoadBuilderLaneGroup,RoadBuilderLaneOption,LaneOptionCombination}.cs`; `RoadBuilder/Domain/Prefabs/LaneGroupPrefab.cs`; `RoadBuilder/Systems/{RoadBuilderNetSectionsSystem,RoadBuilderGenerationDataSystem}.cs`; `RoadBuilder/Systems/UI/RoadBuilderNetSectionsUISystem.cs`; `RoadBuilder/Mod.cs`, `Setting.cs`, `RoadBuilder.csproj`; and `NetworkConfigExtensionsUtil.cs` including the `FindLanes` implementation. That helper skips conditional pieces/subsections, so the closed-template guard checks every piece directly and refuses subsections.

Confirmed external section discovery and category filtering. Groups' Two-way UI does not establish traffic conflict behaviour. Built-in group initialization scans Road Builder's own assembly, so an add-on should register prefabs rather than assume its subclasses will be discovered. No general proposed-layout validator callback was found in the inspected UI/discovery surfaces; further investigation or a narrow upstream event is needed.

## Traffic

[ce1551f19d0341d7a4a6bb126daa515c6c021b43](https://github.com/krzychu124/Traffic/tree/ce1551f19d0341d7a4a6bb126daa515c6c021b43). Read `Code/Mod.cs`, `Code/Traffic.csproj`, README and system directory structure. No root LICENSE was present in the listing inspected; no implementation is copied or relicensed here.

`Mod.OnLoad` disables vanilla `LaneSystem` and installs `TrafficLaneSystem` before it in `Modification4`, with connection/priority synchronization. It registers Road Builder compatibility handling. The README describes unsupported bidirectional connection management and resets after topology changes. A future TWTL lane generator must coordinate with this replacement and respect Traffic's connection choices; source inspection is not a compatibility pass.

## Access Anarchy

[6567d61c0ee39228b032a28aac42ac23a3fc492d](https://github.com/yuexian7/AccessAnarchy/tree/6567d61c0ee39228b032a28aac42ac23a3fc492d). Read README and `Systems/AccessZoneOverlapSystem.cs`; inspected system listing. No root licence was present in the listing inspected; no implementation is copied.

The project edits `LaneOverlap` buffers to remove pedestrian interactions in its scope and documents restoration/rebuild logic, load/save hazards and changes to prior assumptions. It describes targeting 1.6.2f1. These comments do not verify native system ordering. For TWTL, property access and tram/pedestrian conflicts must be revalidated after any overlap edits. Do not rely on the presence of this mod as evidence that vehicle conflicts remain safe in a new corridor.

## Collision Be Gone

[34de05c17e43fb10f004f2a563f79372258f6239](https://github.com/huangjialin1118/CollisionBeGone/tree/34de05c17e43fb10f004f2a563f79372258f6239), MIT. Read `CollisionBeGone/Mod.cs`, `Systems/GhostLaneSpeedIterator.cs`, `Systems/CbgSelectedParkingDumpSystem.cs` and LICENSE.

It disables vanilla `CarNavigationSystem`, installs `GhostCarNavigationSystem` before native navigation `Actions` for game/load simulation, and conditionally skips categories of collision checks. Its iterator references `LaneOverlap`, `LaneObject`, native reservation priorities, curve positions, and overlap/space/speed methods. The fork is a research lead, not authoritative native game source. No fork implementation or purported decompiled game body is copied to this repository.

A functional TWTL cannot assume vanilla braking while that replacement is active. Treat vehicle collision suppression as an unresolved safety conflict; an eventual activation gate needs explicit support or refusal. This research build has no live traffic functionality to activate. Loaded assembly identities are logged, without changing other mods' settings.

## Road Construction Events

[f6b0bea314f21dbc40677f95bcc4021ce05d9867](https://github.com/northernst11-bot/RoadConstructionEvents/tree/f6b0bea314f21dbc40677f95bcc4021ce05d9867), MIT. Read `Mod.cs`, `Systems/RoadConstructionSystem.cs` and LICENSE; inspected systems listing.

Registers construction processing after pathfind `LaneDataSystem`, before `LanesModifiedSystem` in `ModificationEnd`, and a pre-serialization handler. Its implementation has closures, lane blockers, corridor service vehicles and connection-lane processing. A future TWTL must invalidate reservations/route intent for closures and changed access and must not mistake an expected-clearance timeout for actual clearance. No integration is installed.

## Network Tools

[5a4c8dfef850bb76bfa5c3baf75e9c07f6b3679a](https://github.com/lucarager/CS2-NetworkTools/tree/5a4c8dfef850bb76bfa5c3baf75e9c07f6b3679a), MIT in LICENSE.txt. Inspected repository structure, `NetworkTools.Mod/NetworkToolsMod.cs` lifecycle source and LICENSE.txt; tool-specific geometry implementation was not audited.

Road edits, splits, merges and lane regeneration require authoritative owner/revision invalidation rather than retaining old lane entity IDs. This is an integration requirement, not a verified Network Tools event API or tested compatibility claim.

## Curbside

[ebad24a2ed92b6ba2c5a02a52877e6eb204d5f4f](https://github.com/RaftermanNZ/Curbside/tree/ebad24a2ed92b6ba2c5a02a52877e6eb204d5f4f), MIT original source, separate artwork rights. Read README, `Mod.cs`, LICENSE and root listing.

Its registration includes marking themes, paint/curb emission, intersection painting, road attachment transfer, lane-system restore hooks and `ApproachStopSystem` before `LaneOverlapSystem`. It is more than a texture provider. Optional visual cooperation needs regeneration/paint ownership testing and overlap-hook review. No textures or code are copied, and Curbside is not a dependency for traffic.

## Game version and community references

The [official Steam news feed](https://steamcommunity.com/app/949230/allnews) identifies stable 1.6.2f1 Autumn Breeze (September 15, 2026) and a separate performance beta (October 28). The installed Game.dll was unavailable. Version claims from mod READMEs cannot prove binary compatibility.

Opened the supplied [community modding guide](https://ps1ke.github.io/Cities-Skylines-2-Modding-Guide/). It is a discovery/index lead only; no generated documentation signature was accepted as game verification. Native collinear overlap, braking, route-intent classification, reservation execution order and tram collision behaviour remain unresolved.
