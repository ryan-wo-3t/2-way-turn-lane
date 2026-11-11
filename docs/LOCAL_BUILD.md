# Local Windows build and inspection

Use a disposable city and disable any subscribed copy of this development mod. No game assemblies or third-party binaries belong in this repository.

## Prerequisites

1. A licensed Cities: Skylines II PC installation. The owner's installed **1.6.4f1** has now been verified from the version file and game log. See LOCAL_VERIFICATION.md for the exact assembly hashes; do not assume the original 1.6.2f1 signatures.
2. Install the official CS2 Modding Toolchain through the game's options. Keep its `CSII_TOOLPATH`, managed-path and postprocessor environment configuration. Open a fresh PowerShell after installation.
3. Install .NET SDK 8. The verified official imports target **net48**; reference assemblies restore through NuGet. The official postprocessor separately requires a .NET 6 runtime. SDK 8 alone does not satisfy it. An isolated runtime can live under ignored `work/dotnet6`; Build-Local.ps1 uses it if present or accepts `-RuntimeRoot`.
4. Let the matching licensed Unity Editor finish importing/compiling the official Unity mod project. Both `Library/ScriptAssemblies/Unity.Entities.CodeGen.dll` and `Unity.Burst.dll` must exist. A green project-install check alone did not establish this on the owner's machine.
5. Use the installed Road Builder DLL or build Road Builder at the exact inspected commit. The installed 0.8.0.0 DLL was used for the verified 1.6.4f1 build. A source build may also require Node/npm for its UI. Do not commit dependency DLLs.

```powershell
git clone https://github.com/ryan-wo-3t/2-way-turn-lane.git
Set-Location 2-way-turn-lane
git switch codex/twtl-foundation
dotnet --info
dotnet build tests/Twtl.Core.Tests/Twtl.Core.Tests.csproj -c Release
dotnet run --project tests/Twtl.Core.Tests -c Release --no-build
dotnet run --project tests/Twtl.Core.Tests -c Release --no-build -- --experiment
```

To optionally build Road Builder from source in a separate checkout:

```powershell
git clone https://github.com/JadHajjar/RoadBuilder-CSII.git
git -C RoadBuilder-CSII checkout 5f4b1ab5d1a5ce4f8d05e4fc89c354f3a1d53485
# Follow Road Builder's prerequisites and UI build instructions, then:
dotnet build RoadBuilder-CSII/RoadBuilder/RoadBuilder.csproj -c Release
```

The local wrapper discovers the official toolchain through user environment variables and requires exactly one installed RoadBuilder.dll candidate, or an explicit path:

```powershell
./scripts/Build-Local.ps1
# If more than one Road Builder copy exists:
./scripts/Build-Local.ps1 -RoadBuilderAssembly 'C:\actual\path\RoadBuilder.dll'
# After closing the game, explicitly deploy to the official local mod directory:
./scripts/Build-Local.ps1 -Deploy
```

Default builds stage under ignored `artifacts/local-mods/TwoWayTurnLane`; they do not replace the game's development mod. The wrapper checks dependency paths, preserves the exact official reference list, runs official IL/Burst processing through a native argument array, then stages output. This avoids the Windows shell's command-length and ampersand parsing problems. Compilation failures are real compatibility failures, not permission to invent API stubs.

The verified staged output includes `TwoWayTurnLane.dll`, **`Twtl.Core.dll`**, debug symbols and official native platform outputs. Do not ship RoadBuilder.dll or Game/Unity/Colossal DLLs. Enable Road Builder separately. No IDE is required by the verified command-line build. Do not publish.

Record metadata without copying proprietary implementation:

```powershell
./scripts/Inspect-GameApi.ps1 -RoadBuilderAssembly 'C:\actual\path\RoadBuilder.dll'
```

## First local checks

1. Start a new disposable city with only Road Builder and TWTL. Save a baseline. Confirm no extra lane appears with the research opt-in off.
2. Preserve the `TwoWayTurnLane` mod log. `[API]` lists the observed Game.dll version/MVID and signatures. Capture the main-menu display version independently.
3. Enable **Expose closed research corridor**. In Road Builder, find **TWTL Research Corridor CLOSED 3m**. If it never appears, inspect prefab timing/discovery; do not insert it into private dictionaries as a blind workaround.
4. Place a compatible test road with the research section. Verify the section contributes **zero** vehicle lanes and no through paths. This is the expected safe closed behaviour, not a functional TWLTL acceptance result.
5. Select the segment, then enable **Capture selected road once**. The flag resets and logs lane IDs with generations, curves, car lane data, native reservation data, overlap entries and lane objects, with truncation counts, simulation frame and capture time. Up to 32 lane objects also include available native car navigation/current-lane/motion/transform data; these observations do not classify route intent automatically.
6. Test upgrade, deletion, reload and disabling in copies of the disposable save. Record road reconstruction differences; do not assume removal is harmless.

Continue with the bidirectional experiment protocol in [TEST_PLAN.md](TEST_PLAN.md). Do not alter a real city or expose a drivable two-way corridor before native braking, route restrictions and entry control are proven.
