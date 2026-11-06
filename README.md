# Two-Way Turn Lane (TWTL)

An experimental Road Builder add-on for Cities: Skylines II. The goal is one physical centre turn corridor usable from either traffic direction, including asymmetric road layouts and left-hand traffic.

**This is a research implementation, not a functional traffic mod.** Shared-lane driving is unavailable. The opt-in Road Builder entry is explicitly named **TWTL Research Corridor CLOSED 3m** and contains no drivable lanes. It demonstrates external lane discovery without exposing traffic to unverified opposing-lane collision behaviour. Do not use it in an important city.

## Implemented

- A game-independent C# library for physical cross-section eligibility, turn-intent admission, bounded intended distance and safe recovery decisions.
- Conservative spatial reservations with vehicle length and braking clearance, deterministic batch priority, separate simultaneous opposing movements, invalidation and cleanup.
- Predictive merge-gap advice with bounded acceleration/deceleration, trajectory checks and gap preference hysteresis. Advice never authorizes a game lane change.
- Intersection transition planning with a closed fallback for short segments and prohibited turns.
- An external Road Builder research section, a one-shot selected-road geometry/overlap/occupancy inspector, and a reflection-based game API signature audit.
- Thirty-two executable core tests and GitHub Actions verification, without game DLLs or fabricated API stubs.

## Build and verify

With .NET SDK 8 installed:

```sh
dotnet build tests/Twtl.Core.Tests/Twtl.Core.Tests.csproj -c Release
dotnet run --project tests/Twtl.Core.Tests -c Release --no-build
dotnet run --project tests/Twtl.Core.Tests -c Release --no-build -- --experiment
```

This is an executable test harness: `dotnet test` does not run these tests. The process exits nonzero on any failure. The experiment is explicitly an offline model; it does not construct game lanes.

The actual mod requires Windows, a licensed game installation, its official modding toolchain, and Road Builder built from the inspected commit. See [local build](docs/LOCAL_BUILD.md). The game adapter has **not** been compiled or run against game assemblies. Current stable public announcements identify 1.6.2f1; binary compatibility remains unverified. The separate performance beta is outside the research target.

## Development status

Milestone 1 is partially implemented: source verification and the closed lane integration exist; the real game build, live picker verification and automatic configuration hook remain open. Milestone 2 has inspection tools and a test protocol; the opposing logical-lane experiment has not been executed. Later milestones have isolated calculation prototypes, with no traffic actuation.

Read the [implementation report](docs/IMPLEMENTATION_REPORT.md), [architecture](docs/ARCHITECTURE.md), [API evidence](docs/GAME_API_COMPATIBILITY.md), [research](docs/RESEARCH.md), [Road Builder integration](docs/ROAD_BUILDER_INTEGRATION.md), [test plan](docs/TEST_PLAN.md) and [limitations](docs/KNOWN_LIMITATIONS.md) before continuing development.

The existing [MIT licence](LICENSE), copyright Ryan Gordon, is preserved. No game binaries, decompiled game source, Paradox release or production readiness claims are included.
