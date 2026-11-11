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

The original executable core suite passed **32/32** locally and its three-request offline reservation experiment passed. Raw compiler/postprocessor output and the metadata audit are retained under ignored `work/`.

```powershell
./scripts/Build-Local.ps1
./scripts/Inspect-GameApi.ps1 -RoadBuilderAssembly 'actual/path/RoadBuilder.dll'
```

The inspector reads assembly metadata only. It never exports method bodies. Current `Game.Net.LaneOverlap` endpoint fields are bytes; `PrefabSystem.AddPrefab` returns `bool`. Compiler compatibility and member presence establish neither opposing braking semantics nor safe route restrictions.

## Remaining runtime boundary

No TWTL game session, Road Builder picker verification, drivable fixture, braking trial or turn-only route enforcement has passed yet. Traffic remains unavailable. The next step is to load the postprocessed development mod with Road Builder in a disposable city and verify closed-section discovery before attempting a gated native corridor fixture.
