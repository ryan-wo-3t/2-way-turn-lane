# Two-Way Turn Lane (TWTL)

A Cities: Skylines II Road Builder add-on under active development. The goal is a single physical center lane that drivers from either direction use for legal left turns.

## Current build

The mod registers a selectable Road Builder center section and a five-lane road. Their existing prefab names and road ID are retained so the Bonham test save can still resolve them. Its center has an independent two-way path edge. The center-to-junction connectors are blocked where their identity can be verified. The old middle blockage is removed only when it matches the interval this mod installed, and the center has a 5 m/s speed limit.

A new game-side controller checks a car's planned center-lane route before entry. It admits only a visible, short left turn to a building road connection, gives one car at a time an exclusive claim on that physical center edge, and caps the speed of other cars approaching it through the native `CarNavigation` command. It releases a claim after the car leaves the lane or its route disappears. This is the first active driving implementation; **it has not yet passed a live traffic test**. Use only the disposable Bonham test city until legal turns, braking, load behavior, and conflicts are observed in game.

The old diagnostic checkboxes, isolated research roads, one-shot probes, and traffic sampler have been removed. The `TWTL_DRIVABLE_FIVE_LANE_SIDEWALK_TEST_ROAD_V3` ID stays for save compatibility; newly registered roads are named **TWTL Five Lane Road**. Existing saved Road Builder configurations retain their old display name.

## Build

Run `scripts/Build-Local.ps1` on the Windows machine with the official CS2 modding toolchain, installed game, and Road Builder. The current adapter compiled and completed the official Unity IL and Burst processing with zero warnings/errors against the installed CS2 1.6.4f1 and Road Builder 0.8.0.0 assemblies. This verifies assembly compatibility, not traffic behavior.

The [original specification](docs/ORIGINAL_SPECIFICATION.md) remains the requirements. The [known limitations](docs/KNOWN_LIMITATIONS.md) record the remaining work. The older [implementation report](docs/IMPLEMENTATION_REPORT.md) and [local verification](docs/LOCAL_VERIFICATION.md) preserve research history, including options and probes removed from the current build.

Game binaries and local builds are excluded from GitHub. The existing [MIT licence](LICENSE) is retained.
