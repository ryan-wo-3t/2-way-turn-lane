# Game API compatibility evidence

Research date: 2026-11-06. Public stable target: **1.6.2f1**. [Official Steam announcement feed](https://steamcommunity.com/app/949230/allnews) lists the September 15 Autumn Breeze patch and an October 28 separate `performance-test` beta. Public version announcements do not establish the owner's installed binary or API compatibility. Record the actual branch and game display version locally.

**Update 2026-11-10:** The adapter now compiles and completes official IL/Burst postprocessing against the installed **1.6.4f1** assemblies and Road Builder **0.8.0.0**, with 0 warnings/errors. See [installed verification](LOCAL_VERIFICATION.md) for MVIDs, hashes, native review and remaining runtime gates. The original table below records the earlier source-only research; it does not override the installed evidence. No proprietary source or binary is committed.

| API surface | Evidence inspected | Verification level |
| --- | --- | --- |
| `IMod.OnLoad(UpdateSystem)`, `OnDispose()` | Road Builder `Mod.cs` | Public upstream usage only |
| `PrefabSystem.AddPrefab`, prefab `Clone`, `Remove<T>`, `AddOrGetComponent<T>` | Road Builder prefab-generation code | Public upstream usage only |
| `Game.City.CityConfigurationSystem.leftHandTraffic` | Road Builder generation data system | Public upstream usage only |
| `SelectedInfoUISystem.selectedEntity`, `SubLane.m_SubLane`, read-only EntityManager buffers | Collision Be Gone selected parking dump | Public upstream usage only |
| `Curve`, `Game.Net.CarLane`, `LaneOverlap`, `LaneObject`, `LaneReservation` | Public navigation/overlap/dump code | Types and usage observed; exact current layouts unverified |
| `CarNavigationSystem` | Collision Be Gone disables it and schedules before nested `Actions` | Strong conflict evidence; native semantics/order unverified |
| `CarLaneSpeedIterator.CheckOverlappingLanes`, `CheckOverlapSpace`, `CheckSpace`, `UpdateMaxSpeed` | Similarly named methods in third-party `GhostLaneSpeedIterator` | Third-party fork is not authority for exact original signatures or semantics |
| `LaneOverlapSystem`, `NetLaneReservationSystem` | Access Anarchy source and requested audit targets | Exact signatures, fields, scheduling and behaviour unverified |
| `PathfindSetupSystem`, lane access restrictions, navigation route intent | Required investigation targets | No native adapter selected; graph enforcement blocked |

The one-time `ApiAudit` logs Game.dll identity, file version, MVID, targeted type presence, declared fields, selected method signatures and loaded integration assembly identities. It checks both candidate namespaces for `PathfindSetupSystem` and labels missing targets. It does not inspect/copy method bodies and cannot certify full-length collinear overlap support.

## Required local verification before any traffic writes

1. Build with the actual licensed Game.dll and matching official toolchain, not stubs. Confirm Road Builder's pinned project loads with that toolchain. Preserve compiler output.
2. Capture actual game display version, Game.dll file version/MVID and the `[API]` audit. Distinguish version strings from binary identity.
3. Privately inspect native `CarNavigationSystem`, `CarLaneSpeedIterator` overlap/space/speed methods, `LaneOverlapSystem`, `LaneReservation`, `NetLaneReservationSystem`, pathfinding setup and native `SystemOrder`. Record exact signatures, relevant flags, interval units and producer/consumer order in a review note. Do not commit proprietary implementation.
4. Determine whether `LaneOverlap` intervals are monotone and correctly mapped for opposite collinear curves; examine quantization and whether full-length overlap is pruned or treated as a crossing. Test reciprocal intervals and correct physical positions.
5. Observe actual vehicle objects, reservations, priorities, braking distances and stopping points on a disposable closed experiment. An overlap record's presence alone is insufficient.
6. Verify a graph restriction that prevents through routing and a native entry-control/speed path that acts before stopping calculations. Do not set speed after native collision avoidance.

Approach A (native overlaps/reservations) remains the first experiment. The custom controller is an isolated Approach B research prototype, not proof that A failed and not a selected replacement. Approach C navigation patches are not implemented. No engine limitation has been confirmed or rejected by runtime evidence.
