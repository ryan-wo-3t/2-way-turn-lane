# Local Windows build and inspection

Use a disposable city and disable any subscribed copy of this development mod. No game assemblies or third-party binaries belong in this repository.

## Prerequisites

1. A licensed Cities: Skylines II PC installation. Confirm the main-menu version and branch. The public stable research target is 1.6.2f1; the performance beta is not verified.
2. Install the official CS2 Modding Toolchain through the game's options. Keep its `CSII_TOOLPATH`, managed-path and postprocessor environment configuration. Open a fresh PowerShell after installation.
3. Install .NET SDK 8 and the .NET Framework 4.7.2 targeting pack for the source-based adapter target. If the actual toolchain requires another framework, establish it against real assemblies and update the project with compiler evidence; do not invent stubs. `RoadBuilder.csproj` at the pinned commit declares net472/C#9.
4. Build Road Builder at the exact inspected commit. Its build may also require Node/npm for its UI. Follow that project's build instructions; preserve its MIT notice. Do not commit its DLL here.

```powershell
git clone https://github.com/ryan-wo-3t/2-way-turn-lane.git
Set-Location 2-way-turn-lane
git switch codex/twtl-foundation
dotnet --info
dotnet build tests/Twtl.Core.Tests/Twtl.Core.Tests.csproj -c Release
dotnet run --project tests/Twtl.Core.Tests -c Release --no-build
dotnet run --project tests/Twtl.Core.Tests -c Release --no-build -- --experiment
```

In a separate checkout, build Road Builder:

```powershell
git clone https://github.com/JadHajjar/RoadBuilder-CSII.git
git -C RoadBuilder-CSII checkout 5f4b1ab5d1a5ce4f8d05e4fc89c354f3a1d53485
# Follow Road Builder's prerequisites and UI build instructions, then:
dotnet build RoadBuilder-CSII/RoadBuilder/RoadBuilder.csproj -c Release
```

Point to the resulting **actual** DLL and build TWTL from the TWTL checkout:

```powershell
$rbDll = (Resolve-Path 'C:\path\to\RoadBuilder.dll').Path
$env:CSII_TOOLPATH = [Environment]::GetEnvironmentVariable('CSII_TOOLPATH', 'User')
dotnet build src/Twtl.Mod/Twtl.Mod.csproj -c Release "-p:RoadBuilderAssembly=$rbDll"
```

The dependency guard errors if toolchain imports or RoadBuilder.dll are absent. Compilation errors against current assemblies are real compatibility failures to investigate, not permission to create replacement API stubs. This mod build command was not executed successfully in the cloud; the cloud lacks dotnet and game files.

Inspect official postprocessor/deployment output. Stage the resulting `TwoWayTurnLane.dll` **and `Twtl.Core.dll`**, plus any official postprocessor outputs required by that toolchain, in the local development mod directory it reports. If the automatic WIP deploy omits the project-reference DLL, copy `Twtl.Core.dll` from its Release output into that same mod directory after closing the game. Do not ship RoadBuilder.dll or any Game/Unity/Colossal DLLs. Enable Road Builder separately. Do not publish.

## First local checks

1. Start a new disposable city with only Road Builder and TWTL. Save a baseline. Confirm no extra lane appears with the research opt-in off.
2. Preserve the `TwoWayTurnLane` mod log. `[API]` lists the observed Game.dll version/MVID and signatures. Capture the main-menu display version independently.
3. Enable **Expose closed research corridor**. In Road Builder, find **TWTL Research Corridor CLOSED 3m**. If it never appears, inspect prefab timing/discovery; do not insert it into private dictionaries as a blind workaround.
4. Place a compatible test road with the research section. Verify the section contributes **zero** vehicle lanes and no through paths. This is the expected safe closed behaviour, not a functional TWLTL acceptance result.
5. Select the segment, then enable **Capture selected road once**. The flag resets and logs lane IDs with generations, curves, car lane data, native reservation data, overlap entries and lane objects, with truncation counts and capture time.
6. Test upgrade, deletion, reload and disabling in copies of the disposable save. Record road reconstruction differences; do not assume removal is harmless.

Continue with the bidirectional experiment protocol in [TEST_PLAN.md](TEST_PLAN.md). Do not alter a real city or expose a drivable two-way corridor before native braking, route restrictions and entry control are proven.
