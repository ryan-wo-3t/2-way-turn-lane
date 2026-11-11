# Cities: Skylines II — Two-Way Turn Lane
## Complete Codex Cloud Development Specification

### 1. Project objective

You are the lead C# developer of an open-source mod for **Cities: Skylines II**, provisionally named **Two-Way Turn Lane (TWTL)**.

Work directly in the currently selected GitHub repository, `CS2-TwoWayTurnLane`. Preserve its existing files and MIT License. Do not create a separate repository.

Develop this mod primarily as an **add-on for the existing Road Builder mod**, allowing players to add a fully functional North American-style two-way centre turn lane (TWLTL) to custom road configurations.

The objective is to create a genuine shared turning lane, not merely a visually accurate road marking.

Vehicles travelling in opposite directions must be able to use the same physical centre lane for appropriate turning movements, while safely detecting and avoiding conflicting traffic.

Where feasible, reuse Cities: Skylines II's existing traffic simulation, pathfinding, collision detection, braking and lane reservation systems.

The project should eventually be suitable for publication on Paradox Mods and potentially integration into Road Builder itself.

**Begin implementation after verifying the relevant APIs. Do not stop after creating a design document or proposal.**

The initial research target is game version 1.6.2f1, but verify the current game version and compatibility before selecting API signatures.

---

### 2. Primary architecture: Road Builder add-on

Road Builder repository:

https://github.com/JadHajjar/RoadBuilder-CSII

Our mod must integrate with Road Builder rather than implement a separate general-purpose road creation system.

Reuse Road Builder for:

- Road configuration and editing.
- Lane arrangement and selection.
- Road asset generation.
- Lane widths, markings and appearance.
- Road configuration persistence.
- Road upgrades and regeneration.
- Existing road-editing UI.

Investigate Road Builder's custom-lane support, including the currently implemented equivalents of:

- `RoadBuilderLaneInfo`
- `RoadBuilderLaneGroup`
- `LaneGroupPrefab`
- Two-way lane-group configuration.
- Custom network section registration.

Verify all interfaces against Road Builder's current source code.

The shared turn lane should appear as a selectable lane type within Road Builder's normal editing interface.

Prefer an external add-on that does not require modifying Road Builder itself.

If the extension API cannot support a required feature, document the smallest possible upstream enhancement before introducing a fork or invasive patch.

Keep the traffic-management implementation independent of Road Builder-specific types wherever practical.

The eventual objective is to make it easy for Road Builder's developers to integrate our functionality directly if they wish.

---

### 3. Supported road configurations

**Do not restrict the shared lane to roads with odd numbers of lanes or to five-lane roads.**

Support any compatible road with at least three drivable vehicle lanes, including the shared turning lane.

Examples:

- 1 eastbound + shared lane + 1 westbound.
- 2 eastbound + shared lane + 1 westbound.
- 2 eastbound + shared lane + 2 westbound.
- 3 eastbound + shared lane + 1 westbound.
- 3 eastbound + shared lane + 2 westbound.
- Any other compatible configuration supported by Road Builder.

Asymmetric arrangements are explicitly required.

Do not impose an artificial maximum lane count.

Require at least one ordinary vehicle travel lane in each direction.

Do not count sidewalks, parking lanes, bicycle lanes, medians or tram-only tracks as ordinary vehicle travel lanes.

Support one physical shared bidirectional turning lane per road cross-section for the initial release.

The shared lane must occupy a physically accessible central corridor between opposing directions of traffic.

Do not require equal lane counts on either side.

Do not impose restrictions simply because the overall lane count is even or odd.

---

### 4. Traffic handedness

Support both right-hand and left-hand traffic.

For right-hand-traffic cities, such as Canada and the United States:

- The shared centre lane supports left-turn movements.
- Vehicles otherwise travel on the right-hand side of the road.

For left-hand-traffic cities, such as the United Kingdom and Australia:

- The shared lane supports right-turn movements.
- Vehicles otherwise travel on the left-hand side.

Automatically detect the city's traffic handedness using the game configuration.

Use common direction-aware logic rather than implementing two entirely separate traffic systems.

All lane validation, turning, merging, conflict management and intersection transitions must respect traffic handedness.

---

### 5. Physical compatibility validation

The shared turn lane should be available only where the road's physical configuration permits its intended bidirectional turning functionality.

Implement two validation stages:

**Configuration validation:** Evaluate the proposed Road Builder cross-section before generating the road.

**Runtime validation:** Evaluate the placed network, turning connections and relevant infrastructure following road construction or modification.

A valid shared centre turn lane must be physically accessible from both directions and permit the intended turning movements.

#### Invalid configurations

Examples include:

- A raised centre median separating opposing traffic.
- A continuous physical barrier preventing crossing.
- Incompatible geometry that prevents vehicles from one direction accessing the shared lane.
- An infrastructure arrangement that makes the intended turning movements physically impossible.
- Invalid lane topology.
- A road lacking an ordinary vehicle lane in either direction.

A physical centre median and a shared two-way turning lane are mutually exclusive when the median blocks access.

The shared lane option should be disabled or hidden for these configurations where Road Builder's extension interfaces permit.

If the UI cannot disable the option directly, provide a clear error and prevent the unsafe traffic functionality from being activated.

Do not silently generate an unusable lane.

#### Valid configurations

Painted lines are not physical barriers.

The absence of existing buildings, driveways or destinations does not invalidate an otherwise compatible road.

Do not require actual traffic demand before enabling the lane.

#### Tram compatibility

Tram tracks must not automatically invalidate the shared centre lane.

An example configuration is:

`↑ ↑ tram↑ | shared↕ | tram↓ | ↓ | ↓`

The mod should support arrangements where vehicles can physically and legally cross tram tracks to complete their turns.

Investigate whether:

1. Cars can cross the tram track using valid network connections.
2. The track is crossable or physically separated by barriers.
3. The movement is permitted by the network topology.
4. Existing game logic recognizes conflicts between turning vehicles and trams.
5. Additional conflict reservations are needed.
6. Cars can enter and leave the centre lane without interfering unsafely with tram movements.

Where tram infrastructure forms an impassable barrier, the shared lane configuration is invalid.

Do not assume all tram tracks are crossable.

Do not claim complete tram compatibility without testing the actual behaviour.

#### Validation results

Use three outcomes:

**VALID:** The road configuration physically supports the intended bidirectional turning functionality.

**WARNING:** The road configuration is physically compatible, but specific network connections or runtime behaviour remain unverified.

**INVALID:** A physical or technical restriction makes the shared lane fundamentally unusable or unsafe.

A known physical obstruction must be INVALID, not merely a warning.

Where a placed road becomes incompatible after an edit, do not delete the road or unexpectedly change its layout. Disable unsafe shared-lane behaviour and report the problem.

---

### 6. Lane geometry and representation

Investigate how to represent one physical centre lane with two permitted travel directions.

The preferred initial experiment is two directional logical lanes sharing the same physical corridor.

However, this representation must not be assumed to work without verification.

Confirm that:

- Both directional lanes can exist within the network.
- Both appear in the pathfinding graph.
- Vehicles occupy the correct physical centre-lane position.
- Ordinary travel lanes remain correctly connected.
- Opposing vehicles recognize physical conflicts.
- Vehicles do not pass through one another.
- Lane regeneration preserves the intended geometry.
- Vehicles cannot treat either logical lane as an ordinary through lane.

Do not use two complete overlapping road networks as the final solution.

A visual demonstration in which opposing vehicles clip through one another is not sufficient.

If two logical lanes are unsuitable, investigate alternatives and document the technical reason.

---

### 7. Opposing-traffic detection

This is the highest-priority technical challenge.

Investigate the relevant Cities: Skylines II systems, including:

- `Game.Simulation.CarNavigationSystem`
- `Game.Simulation.CarLaneSpeedIterator`
- `CarLaneSpeedIterator.CheckOverlappingLanes`
- `CarLaneSpeedIterator.CheckOverlapSpace`
- `CarLaneSpeedIterator.CheckSpace`
- `CarLaneSpeedIterator.UpdateMaxSpeed`
- `Game.Net.LaneOverlapSystem`
- `Game.Net.LaneOverlap`
- `Game.Net.LaneReservation`
- `Game.Simulation.NetLaneReservationSystem`

Verify all names, signatures, fields, execution order and semantics against the target game assemblies.

Determine whether `LaneOverlap` supports extended opposing collinear lanes and whether vanilla navigation correctly processes their conflict intervals.

Investigate whether existing vehicle logic:

1. Detects oncoming vehicles on overlapping directional lanes.
2. Determines their physical positions correctly.
3. Calculates stopping distances.
4. Applies braking before collisions.
5. Recognizes existing lane reservations.
6. Prevents vehicles entering incompatible occupied areas.
7. Allows simultaneous use of non-conflicting sections.

Do not assume that intersection collision handling automatically supports full-length opposing lane overlaps.

#### Preferred implementation sequence

**Approach A: Existing ECS overlap and reservation systems**

Attempt to use native lane conflict data and vanilla vehicle braking.

**Approach B: Custom shared-corridor reservation manager**

If the existing overlap system is insufficient, implement custom occupancy interval tracking and reservation control.

Integrate its decisions with vanilla navigation wherever possible.

**Approach C: Targeted navigation modifications**

Only if necessary, modify the smallest relevant portion of vehicle navigation.

Avoid replacing the complete navigation system unless less invasive methods have been demonstrated inadequate.

Document why each selected approach is necessary.

---

### 8. Conflict reservations and deadlock prevention

The mod must prevent physically conflicting movements without unnecessarily blocking compatible vehicles.

Do not lock the entire centre lane to one travel direction at a time.

Vehicles travelling in opposite directions should be able to use separate portions simultaneously when their intended movements do not conflict.

A custom reservation controller, if required, should track:

- Vehicle entity.
- Travel direction.
- Physical vehicle length.
- Position along the shared corridor.
- Planned entry position.
- Planned exit position.
- Expected occupancy interval.
- Estimated arrival time.
- Expected clearance time.
- Existing conflicting reservations.
- Safe braking clearance.
- Reservation priority.
- Reservation state.

Use physical position and travel direction rather than lane identity alone.

Two conflicting vehicle movements must not receive overlapping unsafe reservations.

Use deterministic conflict resolution.

Prevent simultaneous entries that could result in a head-on deadlock.

Vehicles must not enter the shared lane unless they have sufficient distance and a valid exit strategy for their intended movement.

Reservation cleanup must handle:

- Successful turns.
- Successful merges.
- Vehicle despawning.
- Route changes.
- Road deletion.
- Invalid connections.
- Save/load.
- Mod disabling.
- Unexpected simulation events.

Never permit reservation state to accumulate indefinitely.

---

### 9. Turn-aware pathfinding

The shared centre lane is exclusively for permitted turning movements.

Vehicles must not use it for:

- Ordinary through traffic.
- Overtaking.
- Passing slower vehicles.
- Avoiding congestion.
- Unnecessary lane changes.
- Long-distance travel without a legitimate turning purpose.

Permitted uses include:

1. Leaving the normal travel lane to turn across opposing traffic into a valid roadside destination.
2. Leaving a property or other legitimate connection and using the centre lane to make an appropriate turning or merging movement.
3. Other movements permitted by actual geometry and applicable traffic handedness.

Investigate the game's:

- `PathfindSetupSystem`
- Pathfinding graph.
- Lane access restrictions.
- Navigation-lane buffers.
- Building access connections.
- Lane connection restrictions.
- Vehicle route intent.
- Routing costs.

Determine whether a vehicle's upcoming movement can be identified reliably.

A vehicle should only enter the shared lane when its intended route requires an eligible turn.

Do not rely on proximity alone.

Prevent the pathfinder from assigning ordinary through routes to the shared lane.

If a movement cannot be classified reliably, use conservative fallback behaviour.

---

### 10. Maximum intended travel distance

Implement configurable maximum intended travel distances to prevent misuse.

Initial defaults:

- Intended maximum centre-lane travel: 50 metres.
- Begin exit planning: 35 metres.
- Begin urgent exit planning: 50 metres.

These values are initial development settings and must remain configurable.

Distinguish between:

- A vehicle approaching its legitimate planned turn.
- A vehicle that missed its turn.
- A vehicle whose destination became inaccessible.
- A vehicle incorrectly using the centre lane.
- A vehicle temporarily unable to exit safely.

A legitimate turning vehicle should not automatically leave the lane simply because it has travelled 50 metres.

The intended distance limit primarily prevents incorrect through-lane use and provides a recovery mechanism.

Never force an unsafe lane change, teleport a vehicle or cause a collision solely to enforce the distance limit.

---

### 11. Active merging and gap selection

When a vehicle needs to leave the shared centre lane but the adjacent normal travel lane is occupied, it must actively seek an appropriate gap.

Do not allow vehicles to continue indefinitely at normal speed while passively waiting for an opening.

Implement:

1. Detection of surrounding vehicles in the target lane.
2. Identification of candidate gaps ahead and behind.
3. Estimation of vehicle speeds and relative positions.
4. Selection of a safe reachable gap.
5. Temporary acceleration or deceleration within vehicle capabilities.
6. Safe lane-change initiation.
7. Confirmation of successful merging.
8. Retry or gap reselection when traffic conditions change.

If the appropriate gap is ahead, the vehicle may accelerate.

If the appropriate gap is behind, the vehicle may decelerate.

Use safe following distances and predicted movement rather than current position alone.

Use hysteresis to prevent unstable switching between gaps.

Do not override collision avoidance.

Do not accelerate into oncoming traffic.

Do not permit unsafe entry into an adjacent occupied lane.

When no safe gap exists, the vehicle should slow or stop as necessary and continue evaluating opportunities.

A safe delay is preferable to a forced collision.

Investigate influencing existing navigation target speeds and lane selection through ECS data before introducing patches.

Do not modify vehicle speeds after vanilla stopping calculations if this creates an overshooting risk.

---

### 12. Intersection transitions

The shared centre lane must terminate as a bidirectional movement corridor before ordinary intersections.

Near each intersection, the centre lane should become a conventional dedicated turning lane for vehicles approaching that intersection, when the intersection permits the corresponding turn.

Use a configurable default transition length of 30 metres.

For right-hand traffic on an east-west road:

- The east intersection approach becomes an eastbound dedicated left-turn lane.
- The west intersection approach becomes a westbound dedicated left-turn lane.

For left-hand traffic, reverse the turning side appropriately.

This must work regardless of road orientation.

Preserve normal intersection behaviour, including:

- Traffic signals.
- Intersection priorities.
- Turning restrictions.
- Existing lane connections.
- Vehicle yielding.
- Pedestrian crossings.

Do not introduce bidirectional through traffic into intersection conflict zones.

Investigate whether Road Builder's network sections and existing CS2 lane composition mechanisms can implement the transitions automatically.

Prefer existing game geometry and connection systems.

For short road segments where the transitions overlap or insufficient space remains for a shared section, use a valid fallback.

Do not generate invalid lane connections.

Where the intersection does not permit the relevant turn, do not create an illegal dedicated turning movement.

---

### 13. Road modifications and network regeneration

The mod must handle changes to an existing road.

Relevant events include:

- Road upgrades.
- Road deletion.
- Road relocation.
- New intersections.
- Intersection removal.
- Segment splitting.
- Segment merging.
- Lane additions and removals.
- Lane-direction changes.
- Road Builder configuration edits.
- Changes to barriers, medians and tram infrastructure.

Revalidate the shared lane when relevant network geometry changes.

Remove stale reservations and invalid references.

Regenerate lane relationships safely.

Do not modify unrelated roads.

Do not delete or replace a player's road without explicit user action.

When a configuration becomes incompatible, disable unsafe behaviour and provide a diagnostic explanation.

---

### 14. Savegame compatibility and recovery

The mod must handle:

- Saving during active centre-lane use.
- Loading saves with previously created shared lanes.
- Loading when some road configurations have changed.
- Invalid or missing lane entities.
- Interrupted reservations.
- Vehicle route changes.
- Disabling the mod.
- Updating the mod to a newer version.

Do not assume transient vehicle reservations must be persisted directly.

Investigate whether transient state should instead be rebuilt after loading.

Provide safe recovery logic.

Do not leave orphaned ECS components, invalid road connections or permanently blocked vehicles.

Clearly document what happens when the mod is disabled or uninstalled while a saved city contains custom Road Builder roads using the shared lane.

Do not claim uninstall safety unless verified.

---

### 15. Compatibility with other mods

Prioritize compatibility with:

**Road Builder**

https://github.com/JadHajjar/RoadBuilder-CSII

**Traffic**

https://github.com/krzychu124/Traffic

**Network Tools**

https://github.com/lucarager/CS2-NetworkTools

**Access Anarchy**

https://github.com/yuexian7/AccessAnarchy

**Collision Be Gone**

https://github.com/huangjialin1118/CollisionBeGone

**Road Construction Events**

https://github.com/northernst11-bot/RoadConstructionEvents

Investigate their relevant source code, architecture and compatibility implications.

Do not automatically depend on every referenced mod.

Road Builder is the intended primary integration dependency.

Other integrations should be optional where possible.

Investigate compatibility with The Curbside Mod for road markings, but do not depend on it for traffic behaviour.

Avoid modifying unrelated mods or overriding their configuration.

If another mod modifies the same vehicle navigation jobs or lane data, detect or document conflicts as appropriate.

Do not claim compatibility without testing.

---

### 16. Performance

The mod must be designed for large cities with hundreds of thousands of residents.

Avoid city-wide scans of all vehicles on every simulation update.

Limit processing to:

- Road segments containing shared turn lanes.
- Vehicles approaching eligible entry points.
- Vehicles currently occupying shared lanes.
- Vehicles interacting with active reservations.
- Vehicles near relevant merging locations.

Use efficient lookup structures, cached geometry and incremental updates.

Consider Unity ECS and Burst-compatible approaches where appropriate.

Respect:

- Unity job scheduling.
- Native collection lifetimes.
- Entity validity.
- System update ordering.
- ECS command-buffer synchronization.
- Read/write dependencies.
- Thread safety.

Avoid excessive allocations and per-frame logging.

Instrument relevant performance costs.

Report measured overhead rather than claiming the mod is lightweight without evidence.

---

### 17. Mod settings

Provide settings through the game's established mod options system.

Include:

- Enable/disable special shared-lane behaviour.
- Maximum intended centre-lane travel distance.
- Distance at which exit planning begins.
- Intersection transition length.
- Shared-lane speed limit.
- Conflict detection.
- Active merging.
- Diagnostic logging.
- Debug visualization.
- Conservative safety fallback.

Use sensible defaults and validate permitted setting ranges.

Settings must not permit clearly unsafe configurations.

If a setting cannot be implemented reliably through the available APIs, document it rather than displaying a nonfunctional option.

---

### 18. Diagnostics and testing tools

Create diagnostics that help identify actual simulation behaviour.

Useful information includes:

- Shared-lane entity identifiers.
- Physical and logical lane geometry.
- Direction of each logical lane.
- Lane overlap intervals.
- Active reservations.
- Vehicle route intent.
- Entry and exit decisions.
- Rejected entry attempts.
- Vehicle blocker information.
- Braking decisions where accessible.
- Merge gap selection.
- Merge failures and retries.
- Invalid road configurations.
- Lane regeneration.
- Unexpected vehicle conflicts.
- System processing time.

Provide an optional debug visualization if supported.

Ideally, show:

- The shared centre corridor.
- Permitted entry and exit points.
- Vehicle intended movements.
- Reserved portions of the corridor.
- Conflicting reservations.
- Relevant tram conflicts.

Keep diagnostics disabled or lightweight by default.

Never introduce significant simulation overhead through debug output.

---

### 19. Source references and API verification

Study the following source projects and relevant implementation patterns:

Road Builder:
https://github.com/JadHajjar/RoadBuilder-CSII

Traffic:
https://github.com/krzychu124/Traffic

Access Anarchy:
https://github.com/yuexian7/AccessAnarchy

Collision Be Gone:
https://github.com/huangjialin1118/CollisionBeGone

Road Construction Events:
https://github.com/northernst11-bot/RoadConstructionEvents

Network Tools:
https://github.com/lucarager/CS2-NetworkTools

Community CS2 modding reference:
https://ps1ke.github.io/Cities-Skylines-2-Modding-Guide/

Do not treat AI-generated or community-generated technical documentation as authoritative without verification.

Check actual upstream code, exact commits, licences and game assembly compatibility.

Document confirmed APIs separately from assumptions.

Do not copy proprietary game source or binaries into the public repository.

Respect third-party licensing and attribution requirements.

---

### 20. Development milestones

#### Milestone 1 — Source verification and Road Builder integration

Inspect current Road Builder source.

Identify supported custom-lane extension points.

Create the initial C# mod project and integration structure.

Implement a selectable shared centre turn-lane type where the APIs permit.

Confirm whether the lane can be represented in Road Builder without changes to Road Builder itself.

Implement initial configuration validation and diagnostics.

Produce a technically valid build wherever dependencies are available.

#### Milestone 2 — Bidirectional lane experiment

Construct a minimal experimental shared-lane corridor.

Inspect generated directional lane entities.

Investigate pathfinding and overlap data.

Determine whether vanilla vehicle navigation can detect opposing traffic and brake appropriately.

Produce instrumented diagnostics and reproducible in-game test instructions.

Do not claim the experiment passed unless executed against the game.

#### Milestone 3 — Safe conflict management

Implement the chosen conflict-management architecture.

Reuse vanilla overlap and reservation logic where feasible.

Add a custom reservation manager where necessary.

Test overlapping occupancy intervals, priorities, deadlock prevention and cleanup.

Do not proceed as if head-on conflict handling is solved merely because the road renders correctly.

#### Milestone 4 — Turn-aware routing

Identify legitimate turning movements.

Allow appropriate centre-lane entry from either direction.

Prevent through traffic and overtaking.

Handle property access and relevant roadside connections.

Add routing restrictions and diagnostics.

#### Milestone 5 — Active merging

Implement distance tracking, gap identification, gap selection, speed adjustment, safe lane-change initiation and failure recovery.

Test acceleration and deceleration strategies.

Test congested traffic and missed turns.

#### Milestone 6 — Intersection transitions

Implement ordinary dedicated turn-lane approaches near intersections.

Support configurable transition lengths and correct traffic handedness.

Handle short segments, prohibited turns and invalid junction geometry.

#### Milestone 7 — Tram and infrastructure compatibility

Implement physical compatibility checks.

Support crossable tram infrastructure where technically viable.

Reject impassable medians and barriers.

Test tram conflicts and mixed road arrangements.

#### Milestone 8 — Stability and performance

Handle road editing, upgrades, deletion, save/load and mod disabling.

Test compatibility with Road Builder and other relevant mods.

Measure simulation overhead.

Complete documentation and packaging.

Do not skip earlier milestones simply because later features are easier to implement.

---

### 21. Acceptance tests

The finished implementation must be evaluated against the following scenarios.

**Basic turning**

1. A right-hand-traffic vehicle travelling eastbound enters the shared lane and turns left.
2. A westbound vehicle performs the corresponding left turn.
3. Both behaviours work with asymmetric road configurations.
4. Appropriate turning works in left-hand-traffic cities.

**Bidirectional conflict handling**

5. Opposing vehicles using separate portions of the lane can operate simultaneously.
6. Vehicles with conflicting intended movements do not occupy the same physical space unsafely.
7. Vehicles brake or yield appropriately for approaching conflicts.
8. Simultaneous reservation requests do not create unsafe occupancy.
9. Vehicles do not pass through one another.

**Routing**

10. Ordinary through traffic does not enter the shared lane.
11. Vehicles cannot use the lane for overtaking.
12. Legitimate left-turning traffic can enter from both directions.
13. Vehicles leaving eligible roadside connections can use the shared lane appropriately.
14. Missed or invalid turns trigger safe recovery.

**Active merging**

15. A vehicle selects an available gap ahead and accelerates safely when appropriate.
16. A vehicle selects an available gap behind and decelerates safely when appropriate.
17. No available gap results in safe yielding or stopping.
18. Vehicles do not indefinitely cruise in the centre lane while waiting for a gap.
19. Gap-selection decisions do not oscillate excessively.

**Road Builder compatibility**

20. Three-lane shared-turn configurations work.
21. Four-lane asymmetric configurations work.
22. Five-lane symmetric configurations work.
23. Larger compatible configurations work without arbitrary lane-count restrictions.
24. Custom lane selection works through Road Builder.
25. Road upgrades and configuration changes do not leave invalid shared-lane state.

**Physical compatibility**

26. A continuous raised centre median invalidates the shared lane.
27. A continuous physical barrier invalidates the shared lane.
28. Ordinary painted lane markings do not invalidate it.
29. Tram tracks that permit crossing do not automatically invalidate the lane.
30. Impassable tram infrastructure prevents incompatible turning movements.
31. Invalid configurations cannot silently activate unsafe traffic behaviour.

**Intersections**

32. Shared bidirectional operation ends before an ordinary intersection.
33. The correct dedicated turn lane is generated for the approaching direction.
34. Short road segments use a valid fallback.
35. Existing traffic signals and priorities remain functional.

**Reliability**

36. Road deletion cleans up transient state.
37. Road modification regenerates valid lane relationships.
38. Save/load does not leave stale reservations.
39. Disabling the mod does not silently corrupt road configuration data.
40. Unrelated vanilla roads retain normal behaviour.

**Performance**

41. Multiple shared-lane roads operate without excessive simulation overhead.
42. Performance measurements are recorded using reproducible scenarios.
43. Debug logging does not create significant overhead when disabled.

Separate automated unit tests from actual in-game runtime tests.

Do not report an in-game acceptance test as passed unless it was genuinely executed.

---

### 22. Repository structure

Create a maintainable public project containing:

- C# source code.
- CS2 mod project and build configuration.
- Road Builder integration.
- Shared-lane traffic systems.
- Validation systems.
- Configuration and settings.
- Diagnostics.
- Unit tests for engine-independent logic.
- README.md.
- LICENSE (preserve MIT).
- CONTRIBUTING.md.
- CHANGELOG.md.
- .gitignore.
- docs/ARCHITECTURE.md.
- docs/RESEARCH.md.
- docs/ROAD_BUILDER_INTEGRATION.md.
- docs/GAME_API_COMPATIBILITY.md.
- docs/TEST_PLAN.md.
- docs/KNOWN_LIMITATIONS.md.
- docs/LOCAL_BUILD.md.
- Suitable CI configuration for tests that can run without proprietary game assemblies.

Organize the implementation into logical modules.

Separate Road Builder integration from vehicle behaviour and traffic reservation logic.

Do not commit proprietary Cities: Skylines II game assemblies, decompiled game source or other commercial binaries.

Do not fabricate API stubs simply to make an otherwise invalid game mod compile.

Test doubles are acceptable only in clearly isolated unit-test projects.

If the cloud environment lacks necessary dependencies, document the limitation and provide reproducible local Windows build instructions.

---

### 23. Development quality requirements

Follow established C# conventions and the current CS2 modding ecosystem's build practices.

Use meaningful names and document complex vehicle-behaviour algorithms.

Avoid unexplained constants.

Make safety-critical calculations independently testable.

Include tests for:

- Reservation overlap.
- Vehicle-length clearance.
- Opposing travel directions.
- Gap selection.
- Safe merging.
- Reservation cleanup.
- Deterministic priority.
- Road eligibility.
- Traffic handedness.
- Invalid infrastructure arrangements.
- Short intersection transitions.

Use actual geometry and vehicle behaviour where required rather than assuming lane centreline overlap is sufficient.

Handle invalid entities and network changes defensively.

Avoid unnecessary Harmony patches.

When patches are unavoidable, document:

- The exact target.
- Why the patch is necessary.
- Expected compatibility impact.
- Applicable game versions.
- Fallback if the target changes.
- How to disable the patch safely.

Do not implement unfinished methods that silently return success.

Do not represent placeholder behaviour as working functionality.

---

### 24. Reporting and independent verification

The project owner will use a separate technical reviewer to inspect your work and verify claims.

At the end of each development milestone, provide:

1. Files created or modified.
2. Relevant commit IDs.
3. Source references inspected.
4. Verified game API signatures.
5. Architectural decisions and rationale.
6. Build commands executed.
7. Actual build results.
8. Unit-test commands and outcomes.
9. In-game tests performed, if any.
10. Tests not performed and why.
11. Remaining technical uncertainties.
12. Known limitations.
13. Performance measurements, if available.
14. Exact next steps for development.
15. Specific files that deserve independent review.

Distinguish:

- Implemented and verified.
- Implemented but not runtime-tested.
- Designed but not implemented.
- Blocked by unavailable dependencies.
- Rejected because of a confirmed engine limitation.

Do not present assumptions as verified facts.

Do not claim a completed functional mod merely because the code compiles.

---

### 25. Immediate execution instructions

Begin work in the currently selected GitHub repository.

First:

1. Inspect the current Road Builder source and extension interfaces.
2. Inspect the relevant CS2 vehicle navigation and lane conflict references.
3. Establish the actual game API compatibility requirements.
4. Create the maintainable mod project structure.
5. Implement the Road Builder custom-lane integration where supported.
6. Implement initial physical compatibility validation.
7. Build diagnostic tools for lane geometry, overlap data and vehicle occupancy.
8. Begin the isolated bidirectional conflict experiment.
9. Add unit tests for engine-independent traffic calculations.
10. Execute available builds and tests.

Continue implementing subsequent milestones wherever verified APIs and available tools permit.

Do not stop after creating a README, a proposed architecture or a TODO list.

If Codex Cloud lacks the game assemblies or a running copy of CS2, complete all implementation and automated testing possible without those dependencies.

Provide exact instructions for any Windows-side compilation or in-game tests the project owner must perform.

At the end of the task, generate a detailed implementation and verification report.

**Do not publish to Paradox Mods, create a release, or claim production readiness until the project owner has tested and approved the implementation.**

The overall objective is a genuinely functional, safe, maintainable two-way centre turn lane that works as a natural extension of Road Builder, accommodates flexible road layouts, and behaves realistically within Cities: Skylines II's existing simulation.