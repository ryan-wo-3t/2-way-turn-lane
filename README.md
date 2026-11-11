# Two-Way Turn Lane (TWTL)

An experimental Road Builder add-on for Cities: Skylines II. The goal is one physical centre turn corridor usable from either traffic direction, including asymmetric road layouts and left-hand traffic.

**This is a research implementation, not a functional traffic mod.** Shared-lane driving is unavailable. The opt-in Road Builder entry is explicitly named **TWTL Research Corridor CLOSED 3m** and contains no drivable lanes. It demonstrates external lane discovery without exposing traffic to unverified opposing-lane collision behaviour. Do not use it in an important city.

## Implemented

- A game-independent C# library for physical cross-section eligibility, turn-intent admission, bounded intended distance and safe recovery decisions.
- Conservative spatial reservations with vehicle length and braking clearance, deterministic batch priority, separate simultaneous opposing movements, invalidation and cleanup.
- Predictive merge-gap advice with bounded acceleration/deceleration, trajectory checks and gap preference hysteresis. Advice never authorizes a game lane change.
- Intersection transition planning with a closed fallback for short segments and prohibited turns.
- An external Road Builder research section, a one-shot selected-road geometry/overlap/occupancy inspector, and a reflection-based game API signature audit.
- Thirty-six executable core tests, including reverse-curve geometry regressions, without game DLLs or fabricated API stubs.
- Native reverse-curve/reciprocal-overlap diagnostics, upcoming route-buffer capture and native blocker observations.

## Build and verify

With .NET SDK 8 installed:

```sh
dotnet build tests/Twtl.Core.Tests/Twtl.Core.Tests.csproj -c Release
dotnet run --project tests/Twtl.Core.Tests -c Release --no-build
dotnet run --project tests/Twtl.Core.Tests -c Release --no-build -- --experiment
```

This is an executable test harness: `dotnet test` does not run these tests. The process exits nonzero on any failure. The experiment is explicitly an offline model; it does not construct game lanes.

The actual mod requires Windows, a licensed game installation, its official modding toolchain, and Road Builder. The adapter now **compiles and completes official postprocessing against the owner's installed 1.6.4f1 assemblies and Road Builder 0.8.0.0**, with 0 warnings and 0 errors. This does not establish working turn-lane traffic. See [local build](docs/LOCAL_BUILD.md) and [installed verification](docs/LOCAL_VERIFICATION.md) for dependency identities and the runtime boundary.

## Development status

Milestone 1 has a verified real game build and live closed-section discovery, picker, placement, placed-owner lane capture and same-process local save/load in a clean Road Builder playset; the automatic configuration hook and lifecycle gates remain open. Milestone 2 has native inspection tools and a test protocol; the opposing logical-lane experiment has not been executed. Later milestones have isolated calculation prototypes, with no traffic actuation. [The original specification](docs/ORIGINAL_SPECIFICATION.md) is the requirements; the installed 1.6.4f1 APIs supersede its initial 1.6.2f1 research target.

Read the [implementation report](docs/IMPLEMENTATION_REPORT.md), [architecture](docs/ARCHITECTURE.md), [API evidence](docs/GAME_API_COMPATIBILITY.md), [research](docs/RESEARCH.md), [Road Builder integration](docs/ROAD_BUILDER_INTEGRATION.md), [test plan](docs/TEST_PLAN.md) and [limitations](docs/KNOWN_LIMITATIONS.md) before continuing development.

The existing [MIT licence](LICENSE), copyright Ryan Gordon, is preserved. No game binaries, decompiled game source, Paradox release or production readiness claims are included.
