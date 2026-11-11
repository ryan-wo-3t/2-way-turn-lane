# Implementation and verification report

## Windows continuation, 2026-11-10

Integrated specification commit `1971f05` without discarding local work and read the full ORIGINAL_SPECIFICATION.md. Verified the owner's installed CS2 **1.6.4f1**, official Unity/toolchain and Road Builder **0.8.0.0**. Commit `6339581` repairs and verifies the complete native build pipeline, including official IL/Burst postprocessing and correct staging order. All outputs remain ignored and outside GitHub.

The continuation adds fail-checked closed-section registration with fresh Road Builder metadata, MainLoop registration so late options changes can be handled, discovery acknowledgement through Road Builder's public catalogue, reverse-curve/reciprocal-overlap diagnostics, native lane-node/blocker/upcoming-path captures, and four geometry regressions. The core suite passes **36/36**. The full mod build reports **0 warnings / 0 errors** against real installed assemblies. Read [LOCAL_VERIFICATION.md](LOCAL_VERIFICATION.md) for commands, binary identities, static native observations and outstanding runtime work.

**Functional shared-lane traffic remains unfinished.** No native collision, turn-only routing, entry enforcement, merging, tram or junction acceptance result follows from this compile. The historical cloud report below remains the record of the original foundation; its unavailable-assemblies and uncompiled-adapter statements are superseded by the Windows verification above.

Date: 2026-11-06. Repository: `ryan-wo-3t/2-way-turn-lane`. Branch: `codex/twtl-foundation`. Review: [draft PR #1](https://github.com/ryan-wo-3t/2-way-turn-lane/pull/1). Original base: `b79d2016ffaae0837170fd3e2a4cf491870a6b2f`. Original Ryan Gordon MIT LICENSE retained verbatim.

## Outcome and completion boundary

Implemented a substantial independent safety-calculation foundation, tests, read-only game diagnostics and an opt-in closed Road Builder lane-discovery experiment. **A genuine functional two-way turn lane is not delivered.** No shared-lane traffic can be activated in this build. Native game APIs, pathfinding and collision behaviour could not be verified without licensed assemblies and a running game.

Source verification confirms that external Road Builder custom sections are supported, but does not confirm a two-direction physical corridor, safe opposing braking or turn-only routing. The appropriate next deliverable is a real Windows compile and instrumented native corridor experiment, not a Paradox release.

## Files created or modified

All files except LICENSE are new relative to the original base.

| Files | Implementation |
| --- | --- |
| `src/Twtl.Core/Twtl.Core.csproj`, `Primitives.cs` | Shared physical coordinates, finite input guards, settings/defaults, handedness and stopping calculations |
| `src/Twtl.Core/RoadValidation.cs` | Cross-section/runtime validation and short/prohibited intersection transition planning |
| `src/Twtl.Core/RoutePolicy.cs` | Turn-purpose admission, direction/distance checks and safe recovery decisions |
| `src/Twtl.Core/Reservations.cs` | Physical interval controller, deterministic batch priority, body/braking clearance, occupied safety holds and cleanup |
| `src/Twtl.Core/GapSelection.cs` | Bounded acceleration/deceleration advice, whole-horizon predicted separation and stable-gap preference |
| `src/Twtl.Mod/Twtl.Mod.csproj`, `Mod.cs`, `Settings.cs` | Real-toolchain project, mod lifecycle and implemented research options only |
| `src/Twtl.Mod/RoadBuilderIntegration/ResearchLaneSystem.cs` | External, closed, non-drivable custom section registration; no fork |
| `src/Twtl.Mod/Diagnostics/ApiAudit.cs`, `SelectedRoadDiagnosticsSystem.cs` | Assembly/member audit and selected-road geometry/overlap/reservation/object capture |
| `tests/Twtl.Core.Tests/Twtl.Core.Tests.csproj`, `Program.cs` | Executable core tests and offline opposing-reservation experiment |
| `.github/workflows/core.yml`, `Directory.Build.props`, `.gitignore` | .NET core CI and build conventions; proprietary/binary outputs ignored |
| `README.md`, `CONTRIBUTING.md`, `CHANGELOG.md` | Status, commands and contribution requirements |
| `docs/ARCHITECTURE.md`, `RESEARCH.md`, `ROAD_BUILDER_INTEGRATION.md`, `GAME_API_COMPATIBILITY.md` | Boundaries, pinned evidence and API uncertainty |
| `docs/LOCAL_BUILD.md`, `TEST_PLAN.md`, `KNOWN_LIMITATIONS.md`, `IMPLEMENTATION_REPORT.md`, `research-sources.json` | Reproducible continuation instructions and verification ledger |

## Commits and validation evidence

Initial implementation commit: `4c4642a61859e5a992b4831059496d895f6d8988`. Its [PR CI run](https://github.com/ryan-wo-3t/2-way-turn-lane/actions/runs/42213687763), job `127878296919`, built successfully with SDK 8.0.427, **0 warnings / 0 errors**, and reported **30 passed / 0 failed**. The offline model granted two disjoint opposing movements and rejected the conflicting third.

Core verification commit: `390cba4319ca8834e6f511b87488d05d6406bf17`. Its [CI run](https://github.com/ryan-wo-3t/2-way-turn-lane/actions/runs/42214789890), job `127881991015`, built with **0 warnings / 0 errors** and reported **32 passed / 0 failed**, including the added entry-expiry and occupied-invalidation regressions. The offline experiment also passed. Later changes expand the uncompiled, read-only game inspector and record this evidence; the core and tests are unchanged from that verified revision. Final branch head is available in PR #1.

Commands executed by that CI job:

```sh
dotnet build tests/Twtl.Core.Tests/Twtl.Core.Tests.csproj -c Release
dotnet run --project tests/Twtl.Core.Tests -c Release --no-build
dotnet run --project tests/Twtl.Core.Tests -c Release --no-build -- --experiment
```

In the cloud workspace, the first build command exited 127 (`dotnet: command not found`). `command -v`/filesystem inspection found no dotnet/csc/mcs. Git clones of the target and six source repositories and an SDK download HEAD request failed with connection refusal at the configured proxy. Work continued through the connected GitHub source/write/Actions tools, rather than fabricating local build success. No game build was attempted as if its dependencies existed.

## Milestone ledger

| Milestone | Implemented | Not completed / why |
| --- | --- | --- |
| 1: verification/integration | Current Road Builder source/API research, maintainable real-toolchain C# project, closed discovery adapter, initial core validation and diagnostics | Actual Windows compile/live picker and automatic pre-generation validation hook need game/toolchain and further upstream API investigation |
| 2: opposite lane experiment | API audit, selected-road inspector, offline reservation experiment and reproducible native test protocol | No real opposing lane entities/graph/overlap/braking experiment; proprietary assemblies/game unavailable |
| 3: safe conflict management | Tested isolated conservative interval reservation controller with deterministic admission, clearance and lifecycle | Native entry enforcement and A-versus-B selection remain unverified; no controller drives game vehicles |
| 4: turn-aware routing | Tested explicit-intent policy, handedness, distance and recovery | Route-intent extraction and pathfinding graph restrictions unavailable |
| 5: active merging | Tested predicted-gap advice and bounded acceleration/deceleration candidates | Native speed, collision and lane-change actuation unavailable |
| 6: intersections | Tested transition intervals and closed short-segment fallback | No generated lane geometry or legal connection integration |
| 7: trams/infrastructure | Core physical validation distinguishes permeable paint/crossable trams from barriers | No actual car/tram topology or collision test |
| 8: stability/performance | Prototype invalidation/reset/despawn/route cleanup, documented disposal and performance protocol | Native lifecycle, save/load/removal, combined mod compatibility and large-city measurements unavailable |

Later calculation prototypes are useful preparatory implementation, not evidence earlier native milestones passed.

## Architectural decisions

1. Keep traffic calculations independent of Road Builder/game types so upstream integration is possible and unit tests run without proprietary dependencies.
2. Use a non-drivable closed section for current external lane discovery. Do not give cars ordinary through edges while turn-only restrictions and native conflict handling are unverified.
3. Keep Approach A as the first native experiment. An independent Approach B model does not establish native overlap inadequacy. No navigation patch or complete replacement is justified yet.
4. Reserve full physical swept intervals, with body/reaction/braking padding. Separate portions can operate simultaneously; predicted time does not release an actual occupant. This sacrifices throughput for a conservative model and needs native enforcement.
5. Produce merge advice only; never overwrite vehicle speeds after vanilla stopping calculations or force a lane change.
6. Expose only working research controls. Core traffic settings are validated programmatic options, not misleading player UI switches.

## Sources and verified signatures

See [RESEARCH.md](RESEARCH.md) and `research-sources.json` for exact pinned commits, inspected files, licences and implications across Road Builder, Traffic, Access Anarchy, Collision Be Gone, Road Construction Events, Network Tools and Curbside. The supplied community guide was opened only as a research index. Public official Steam announcements support stable 1.6.2f1 as the research target, not binary compatibility.

Confirmed Road Builder source surfaces include lane-info/group metadata, options/variant fields, section discovery, public generation dictionaries, custom-content refresh and its lane-finding helper. **No game API signatures or native execution order were verified against actual game assemblies.** The inspector/audit remain implemented but not game-compiled or runtime-tested.

## Tests not performed

All 43 in-game acceptance scenarios in TEST_PLAN.md are NOT RUN. This includes routing in both directions/handedness, actual opposing braking/body separation, tram conflict handling, geometry regeneration, junction signals, load/disable/uninstall and large-city overhead. No complete mod compatibility claim, uninstall-safety claim, verified engine limitation or production-readiness claim is made.

The first model run measured 4.838 ms and the 32-test revision measured 5.434 ms for a three-request cold batch including first-call JIT on their GitHub runners. This is not a warmed benchmark, controller budget or game overhead. No simulation performance measurements exist. Diagnostics measure their own capture duration only when used locally.

## Exact next steps and independent review priorities

1. Follow LOCAL_BUILD.md on Windows. Build the pinned Road Builder and TWTL with actual licensed dependencies; return the compiler output and game display version/MVID. Review `Twtl.Mod.csproj`, `Mod.cs` and `ResearchLaneSystem.cs` first for actual toolchain/load-order compatibility.
2. Confirm external closed-lane discovery and zero vehicle lanes. Test research road regeneration and missing-mod load behaviour only in disposable saves. Investigate a minimal Road Builder validation/post-generation event before a drivable picker entry.
3. Review native overlap, reservation and navigation producer/consumer ordering privately. Use the audit and selected-road capture as observation tools, not compatibility certification.
4. Implement the minimal isolated native fixture using verified piece/lane signatures; execute TEST_PLAN.md Milestone 2. Determine whether native opposing collinear overlaps actually brake correctly. Record exact counterexamples before choosing Approach B or C.
5. Implement route-intent extraction and graph restrictions plus enforceable pre-entry reservation gating. Reconcile real occupancy on load/edit; only then attach `ReservationManager`. Independently review `Reservations.cs` for physical-coordinate mapping, envelope containment, expiry versus occupancy and authority of cleanup observations.
6. Review `GapSelection.cs` constant-speed assumptions, conservative trajectory envelope, bounded candidates and the absence of any native lane-change authorization before connecting to game speeds.
7. Add actual junction generation, tram conflict tests, lifecycle/mod-compatibility tests and measured performance. Seek owner test and approval before any release.

No Paradox Mods publication, release creation, merge or activation of unsafe experimental traffic was performed.
