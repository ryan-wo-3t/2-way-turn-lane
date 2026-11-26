[CmdletBinding()]
param(
    [string]$RoadBuilderAssembly,
    [string]$RuntimeRoot,
    [switch]$Deploy
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$sdk = Get-Command dotnet -ErrorAction SilentlyContinue
$dotnet = if ($sdk) { $sdk.Source } else { Join-Path $env:ProgramFiles 'dotnet/dotnet.exe' }
if (!(Test-Path -LiteralPath $dotnet -PathType Leaf)) { throw 'Install .NET SDK 8.' }
$toolPath = [Environment]::GetEnvironmentVariable('CSII_TOOLPATH', 'User')
if (!$toolPath -or !(Test-Path -LiteralPath (Join-Path $toolPath 'Mod.props'))) { throw 'Install the official in-game modding toolchain.' }
if ($Deploy) {
    if (Get-Process Cities2 -ErrorAction SilentlyContinue) { throw 'Close CS2 before replacing the local development mod.' }
    $localMods = [Environment]::GetEnvironmentVariable('CSII_LOCALMODSPATH', 'User')
    if (!$localMods -or ![IO.Path]::IsPathRooted($localMods)) { throw 'The official local mods directory must be an absolute path.' }
    $localMods = [IO.Path]::GetFullPath($localMods).TrimEnd('\', '/')
    $deployment = [IO.Path]::GetFullPath((Join-Path $localMods 'TwoWayTurnLane'))
    if ((Split-Path $deployment -Parent) -ne $localMods) { throw 'Deployment must remain inside the official local mods directory.' }
    Write-Host "Official WIP deployment will replace only $deployment"
}
if (!$RoadBuilderAssembly) {
    $modsPath = [Environment]::GetEnvironmentVariable('CSII_PDXMODSPATH', 'User')
    if (!$modsPath) { throw 'Pass -RoadBuilderAssembly with the actual installed or locally built RoadBuilder.dll.' }
    $candidates = @(Get-ChildItem -LiteralPath $modsPath -Filter RoadBuilder.dll -File -Recurse)
    if ($candidates.Count -ne 1) { throw "Found $($candidates.Count) RoadBuilder.dll candidates; pass -RoadBuilderAssembly explicitly." }
    $RoadBuilderAssembly = $candidates[0].FullName
}
$RoadBuilderAssembly = (Resolve-Path -LiteralPath $RoadBuilderAssembly).Path
if (!$RuntimeRoot -and (Test-Path -LiteralPath (Join-Path $repo 'work/dotnet6/dotnet.exe'))) {
    $RuntimeRoot = Join-Path $repo 'work/dotnet6'
}
$previousRoot = $env:DOTNET_ROOT
try {
    if ($RuntimeRoot) { $env:DOTNET_ROOT = (Resolve-Path -LiteralPath $RuntimeRoot).Path }
    $buildArguments = @('build', (Join-Path $repo 'src/Twtl.Mod/Twtl.Mod.csproj'), '-c', 'Release',
        "-p:CSII_TOOLPATH=$toolPath", "-p:RoadBuilderAssembly=$RoadBuilderAssembly")
    if (!$Deploy) { $buildArguments += "-p:LocalModsPath=$(Join-Path $repo 'artifacts/local-mods')" }
    Write-Host "Building against $RoadBuilderAssembly"
    & $dotnet @buildArguments
    if ($LASTEXITCODE -ne 0) { throw "Mod build failed with code $LASTEXITCODE" }
} finally {
    $env:DOTNET_ROOT = $previousRoot
}
