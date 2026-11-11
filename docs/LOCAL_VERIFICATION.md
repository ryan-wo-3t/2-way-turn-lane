# Installed 1.6.4f1 verification

Verified on Windows on 2026-11-10. This supersedes the original cloud report's lack of licensed assemblies, not its untested gameplay claims.

## Installed dependencies

- Steam CS2 installation: `C:\Program Files (x86)\Steam\steamapps\common\Cities Skylines II`; Steam build ID `26404411`.
- The local `version` file and `Player.log` both report `1.6.4f1 (1097.158a) [6360.18886]`.
- Official toolchain: `%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\.cache\Modding`.
- Unity Editor `2022.3.62f2`, Entities `1.3.10`; official imports select **net48**.
- .NET SDK `8.0.428`. The official postprocessor requires the separate .NET 6 runtime. A workspace-only `6.0.36` runtime was installed under ignored `work/dotnet6`; no machine environment variables were changed.
- Installed Road Builder: `%CSII_PDXMODSPATH%\pdx_mods\87190_43\RoadBuilder.dll`, assembly version `0.8.0.0`. The mod log confirms this assembly loaded. This build uses the actual installed DLL; it does not claim that DLL is byte-identical to a local source build.
- Public Road Builder checkout in ignored `work/RoadBuilder-CSII` resolves to the original research commit `5f4b1ab5d1a5ce4f8d05e4fc89c354f3a1d53485`.

| Assembly | MVID | SHA256 |
| --- | --- | --- |
| Game.dll | `46198354-d4f9-4d23-bf32-7e3c085d8b92` | `F076DF4B70AA0F21A80345E03A052CA2273BC6754F41DAB7E63D837837A43745` |
| Unity.Entities.dll | `bfb47e8c-9b6e-4acf-8c6d-c80a7bff1c41` | `7E6D9ED7DF5295652FCCDB609D3D092CABB5A3FDB91336702D77381A1D42ED86` |
| RoadBuilder.dll | `ce2803e9-278b-40de-8a06-f4c3688e9f8a` | `7A35FAAAA9E5EB922FADCB005B41CAAE57BFE54DC5496C59739B3CE043C13A06` |

## Build evidence and repairs

The existing mod code compiled against the installed assemblies without C# API errors. The complete official pipeline then exposed prerequisites hidden by the toolchain's green checks:

1. The postprocessor's `net6.0` runtime was missing; SDK 8 alone does not provide it.
2. The official Unity project existed but had no compiled `Unity.Entities.CodeGen.dll` or `Unity.Burst.dll` in `Library/ScriptAssemblies`. Running the matching licensed Editor in batch mode completed the import and compilation.
3. The original official `Exec` command exceeded the Windows shell command limit, with ampersands in profile paths contributing parsing failures. The project now invokes the same official processor and exact reference list through a short PowerShell wrapper and native argument array. Postprocessing explicitly precedes WIP staging.

`scripts/Build-Local.ps1` now builds and stages under ignored `artifacts/local-mods/TwoWayTurnLane` by default; `-Deploy` explicitly selects the official local game mod directory. No game or dependency DLL is staged or committed. The official processor completed IL processing and Burst outputs for Windows, macOS and Linux. The verified build reported **0 warnings / 0 errors**. These platform outputs are build evidence, not platform gameplay acceptance.

The original executable core suite passed **32/32** locally and its three-request offline reservation experiment passed. The continuation adds four reverse-cubic geometry regression cases; the expanded suite passes **36/36**. Raw compiler/postprocessor output and the metadata audit are retained under ignored `work/`.

```powershell
./scripts/Build-Local.ps1
./scripts/Inspect-GameApi.ps1 -RoadBuilderAssembly 'actual/path/RoadBuilder.dll'
```

The inspector reads assembly metadata only. It never exports method bodies. Current `Game.Net.LaneOverlap` endpoint fields are bytes; `PrefabSystem.AddPrefab` returns `bool`. Compiler compatibility and member presence establish neither opposing braking semantics nor safe route restrictions.

## Private native review

Installed Game.dll was inspected locally with ILSpyCmd 9.1.0.7988. Decompiled files remain exclusively under ignored `work/native`; none are staged or published. These are observations from this binary, not a claim of successful vehicle trials:

- `LaneOverlapSystem`'s pair-generation path skips ordinary edge-to-edge lane pairs. Therefore two opposite edge lanes in the same corridor must not be assumed to receive automatic reciprocal overlap records. This is a specific generation-path restriction, not proof that manually supplied overlap data cannot work.
- `CarLaneSpeedIterator.CheckOverlappingLanes` consumes byte endpoints as normalized curve parameters and reads other-lane object buffers and reservation priorities. It uses overlap flags, direction and parallelism in its speed calculations. Parameter distance is not a general physical arc-length coordinate.
- `CheckOverlapSpace`, `CheckSpace` and `UpdateMaxSpeed` exist with signatures captured by the local metadata audit. Overlap presence alone does not prove useful opposing braking or conflict admission.
- `Game.Common.SystemOrder` registers overlap generation in `Modification4B`, pathfinding setup in `MainLoop`, and navigation/actions, vehicle movement and lane-reservation maintenance in simulation phases. The explicit navigation-actions dependency is visible. Registration order alone is not full job-dependency or custom-mod ordering proof.
- Native reservation maintenance is frame-bucketed and updates next/previous reservation data. An independent managed hold must not be written blindly into this transient native state.

The added one-shot selected-road inspector reports lane nodes and edge/node classifications, candidate reversed cubic pairs, both directions of native overlap records (with captured/total counts), upcoming path elements from `PathOwner.m_ElementIndex`, navigation lanes and native blocker data. It makes no traffic writes and cannot classify turn purpose or certify braking.

## Initial live load observation

The owner launched CS2 through Steam. At local log time `2026-11-10 20:46:10`, the game loaded `Twtl.Core`, the official Windows Burst output and `TwoWayTurnLane` successfully. The `[API]` audit ran and identified Road Builder 0.8.0.0. This session used an intermediate continuation build deployed before the final blocker/path-owner additions; those later additions are build-verified separately. Existing playset mods remained enabled, so this is not an isolated compatibility baseline. In a new disposable city, the opt-in option appeared and registered the closed section, but a Road Builder picker search for `tw` returned no section. `Player.log` recorded Unity JSON errors for the cloned Road Builder lane-info component's `RoadCategory` enum. The subsequent fix constructs a fresh section from validated lane-free pieces and orders registration before `PrefabSystem`.

## Clean-playset live retest

On 2026-11-10, Skyve showed the active `2wtl - dev` playset with exactly two enabled mods: Road Builder 0.8.0.0 and Two Way Turn Lane (LOCAL). The latter was already present through official local deployment; no Paradox subscription was needed. The deployed `TwoWayTurnLane.dll` hash matched the last successful official build output. CS2 1.6.4f1 loaded both mods and the Windows Burst output at local log time `21:29:04`.

At `21:29:19.031`, the add-on registered the closed section; at `21:29:19.174`, Road Builder's public `NetSections` catalogue contained it. In a new disposable RHT city on Sweeping Plains, the Road Builder picker returned **TWTL Research Corridor CLOSED 3m** for search `tw`. The section was placed between the opposing pairs of car lanes in a Road Builder road. The UI width changed from **24 m to 27 m**, and Road Builder wrote a local `RoadConfig` version 5 with seven ordered sections: sidewalk, car, car, **TWTL Research Corridor CLOSED 3m**, car, car, sidewalk. This proves catalogue discovery, picker visibility, cross-section insertion and config persistence for the closed section. The observed road geometry widened in the city. It does not prove a native lane-entity count or save/load safety.

`Player.log` showed no recurrence of the unsupported-enum serialization error or new exception during this retest. Road Builder logged one presentation warning: the custom section has no lane icon, so the lane bar falls back to the prefab icon. The one-shot ECS inspector ran, but normal game clicks did not populate `SelectedInfoUISystem.selectedEntity` for this road; it logged `Select a road segment with SubLane data first.` No actual lane topology capture was obtained. The research section still uses only source pieces checked to contain no `NetPieceLanes`; vehicle behavior remains closed. A read-only fallback to Road Builder's public `RoadBuilderUISystem.WorkingEntity` compiled against the installed assemblies with zero warnings and errors after this session; it still requires deployment and live retesting.

## Remaining runtime boundary

The corrected registration, picker and closed cross-section placement passed in the clean playset. Native lane-entity capture, save/load, drivable fixture, braking trial and turn-only route enforcement have not passed. Traffic remains unavailable. The next step is to obtain an owner-level lane topology capture for a placed closed section, then design the separately gated native opposing-corridor fixture.
