# Verification plan

## Automated core tests

Run the three README commands. The executable harness has 36 named cases, including four reverse-cubic geometry regressions, a seeded 500-request invariant exercise and 40 combinations of lane counts/handedness. It checks touching boundaries, invalid numbers/settings, ordinary-lane classification, obstructions, paint/trams, unknown/failed runtime evidence, turn intent, LHT/property exits, planned distance, valid-turn recovery, conflict separation, body/braking padding, deterministic ordering, duplicate requests, expiry, route cancellation, occupied safety holds, deletion/reset, stale revisions, transition overlap and predicted acceleration/deceleration/stable gaps.

The model experiment submits three requests to a single physical corridor: forward `[50,80]`, backward `[180,150]` and backward `[90,60]`. With 4 m vehicles, 2 m/s entry speed, 4 m/s² braking, 1.5 s reaction and 2 m standstill clearance, the first protected interval is `[46,87.5]` and the second `[142.5,184]`. Both are granted; the third conflicts and is rejected. The printed timing includes first-call JIT and is not game overhead.

These tests do not create Unity entities, call native navigation or validate a Road Builder road. Never map a model PASS directly to an in-game acceptance PASS.

## Milestone 1 local gate

Follow LOCAL_BUILD.md. Preserve actual compiler output and `[API]` audit. Verify no registration by default, then opt in and verify the explicitly CLOSED entry appears. Inspect that it has no drivable car/track lanes and that original lane prefabs are unchanged. Test late prefab discovery, both traffic handedness settings, research toggle semantics and Road Builder regeneration. If validation cannot run before generation, keep live traffic unavailable and document the missing callback. The 1.6.4f1 clean-playset test has passed opted-in discovery, picker search, closed-section insertion, Road Builder config persistence, placed-owner native capture and same-process local save/load of an isolated closed road. Restart, removal, moving-car and native opposing-lane gates remain open (LOCAL_VERIFICATION.md).

## Milestone 2 native opposing-lane experiment — NOT EXECUTED

Prerequisites: actual game assembly/ordering review and a reviewed local experimental fixture generator using verified lane/piece signatures. Such a generator is not implemented in this branch. Do not turn the closed template into a drive lane by guessing fields.

1. Create a disposable level straight 200 m road, with normal travel each way and one 3 m physical middle corridor. No junction transitions, trams or destinations yet. Separately construct 3-, 4- and 5-lane layouts; later rotate/curve the road and reverse its segment orientation.
2. In the fixture, create opposite directional logical lanes in the same middle corridor, with entry control initially closed. Confirm two distinct logical entities, shared physical Bézier geometry in reverse parameterization, normal lane continuity and both graph edges. Log owner and lane generations/revisions. Never use overlapping full road networks.
3. Capture reciprocal `LaneOverlap` intervals, flags, native reservation priorities and actual lane objects. Establish normalized/quantized parameter mapping to physical metres and whether native overlap generation covers the full collinear corridor.
4. Only under a verified closed-entry test harness, admit one vehicle at a time toward an eligible planned exit, then two vehicles on separate portions. Record paths, object positions, target/max speeds, braking points, blocker identities and clearance. Repeat with conflicting simultaneous entries, unequal lengths/speeds and a stationary occupant.
5. Require physical body separation, braking before collision, no pass-through, no head-on deadlock and successful legal exits. An overlap buffer or correctly rendered road alone is not success. Pause and close admissions on an invariant failure.
6. With entries closed again, upgrade, split, merge and delete segments; rebuild and compare entity/overlap mappings. Repeat save/load with active occupancy. Verify old logical lanes cannot retain route paths after invalidation.

Select Approach A only if native braking/reservations and route restrictions satisfy these observations. If A is inadequate, record the exact counterexample, then connect the custom interval manager through a verified pre-navigation entry/speed interface. Do not jump to replacing navigation. If no safe interface exists, remain closed and report that blocker; no engine limitation is confirmed yet.

## In-game acceptance ledger

Every scenario below remains **NOT RUN** unless explicitly updated with live evidence. The Windows continuation now has a real compiled/postprocessed 1.6.4f1 build; a verified drivable fixture remains unavailable. These are release gates, not unit-test results. See LOCAL_VERIFICATION.md for narrower build and load evidence.

| ID | Required in-game scenario | Current blocker |
| --- | --- | --- |
| 1 | RHT eastbound eligible left turn | Routing/native adapter |
| 2 | RHT westbound eligible left turn | Routing/native adapter |
| 3 | Asymmetric road turns | Game fixture |
| 4 | LHT corresponding right turns | Game fixture |
| 5 | Opposing use of separate portions | Native conflict experiment |
| 6 | No conflicting unsafe occupancy | Native conflict experiment |
| 7 | Native braking/yield before conflicts | Native conflict experiment |
| 8 | Simultaneous requests remain safe | Entry enforcement adapter |
| 9 | Vehicles never clip through each other | Native conflict experiment |
| 10 | Through traffic cannot route into corridor | Pathfinding restriction |
| 11 | Overtaking cannot use corridor | Pathfinding restriction |
| 12 | Legitimate turns enter from both directions | Route intent extraction |
| 13 | Property exit/merge movements work | Roadside connection extraction |
| 14 | Missed/invalid turn safe recovery | Native recovery adapter |
| 15 | Reachable forward gap and bounded acceleration | Native merge adapter |
| 16 | Reachable rear gap and bounded deceleration | Native merge adapter |
| 17 | No gap yields/stops | Native merge adapter |
| 18 | No indefinite normal-speed waiting in corridor | Native recovery adapter |
| 19 | No unstable gap oscillation in changing traffic | Runtime merge observations |
| 20 | Three drivable lane layout | Drivable prefab fixture |
| 21 | Four drivable lane asymmetric layout | Drivable prefab fixture |
| 22 | Five drivable lane layout | Drivable prefab fixture |
| 23 | Larger compatible layouts | Drivable prefab fixture |
| 24 | Road Builder custom lane selection | Closed research section is picker-selectable, but functional drivable TWLTL selection remains unavailable |
| 25 | Upgrades/configuration edits clean state | Native lifecycle adapter |
| 26 | Raised median prevents activation | Automatic classification/hook |
| 27 | Physical barrier prevents activation | Automatic classification/hook |
| 28 | Painted markings remain eligible | Automatic classification/hook |
| 29 | Verified crossable trams remain eligible | Tram topology/conflict experiment |
| 30 | Impassable tram arrangements rejected | Automatic classification/hook |
| 31 | Invalid layout never activates unsafe traffic | Live activation gate (unavailable) |
| 32 | Bidirectional operation ends before junction | Geometry/connection generation |
| 33 | Correct dedicated turn approach | Geometry/connection generation |
| 34 | Short segment fallback is valid | Game geometry verification |
| 35 | Signals/priorities/crossings preserved | Native junction tests |
| 36 | Road deletion cleans live transient state | Native lifecycle adapter |
| 37 | Edits rebuild correct lane relationships | Native lifecycle adapter |
| 38 | Save/load reconciles interrupted occupancy | Native lifecycle adapter |
| 39 | Disabling mod does not corrupt road data | Disposable-save removal tests |
| 40 | Unrelated roads behave normally | Runtime regression test |
| 41 | Many shared roads within measured budget | Functional adapter/benchmark city |
| 42 | Reproducible performance measurements | Functional adapter/benchmark city |
| 43 | Debug disabled has minimal measured overhead | Functional adapter/benchmark city |

## Extended physical and compatibility matrix

Test raised/flat/painted/tram-separated strips on each side, crossings with and without barriers, short curved segments, slopes and junction prohibited turns. Lack of buildings is not an invalidity. Verify actual car/tram/pedestrian conflicts independently; do not globally suppress collision systems.

After the baseline, test one optional mod at a time: Traffic, Network Tools, Access Anarchy, Collision Be Gone, Road Construction Events and Curbside. Record exact revisions/settings and repeat the failure-sensitive gates. Collision suppression must not silently bypass the activation gate. Respect user signals, lane connectors and road edits. Finally test the combined playset; independent source review does not establish combined compatibility.

## Performance protocol

Use the same save, camera, simulation speed, population, game branch and playset. Compare mod disabled/enabled with diagnostics off, then capture explicitly. Include no eligible roads, 10/100/1000 corridors, congested approaches, opposing turns, trams and concurrent road edits. Warm up, record at least five equal simulation windows, report median/p95 controller time, request count, active/overdue holds, allocations, job stalls and frame/simulation throughput. Include core indexes and cleanup rates. Do not extrapolate the three-request offline cold timing to a city.

Store exact commands, compiler outputs, logs, screenshots/video, timestamps, versions and acceptance IDs. Do not publish saves or proprietary DLLs without appropriate permission. Return the native API review and experiment evidence for independent review before adding traffic actuation.
