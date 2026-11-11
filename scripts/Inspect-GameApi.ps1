[CmdletBinding()]
param(
    [string]$ManagedPath = [Environment]::GetEnvironmentVariable('CSII_MANAGEDPATH', 'User'),
    [string]$RoadBuilderAssembly
)

$ErrorActionPreference = 'Stop'
# Metadata only: never loads Game.dll into a game world or exports method bodies.
Add-Type -Path (Join-Path $ManagedPath 'Colossal.Mono.Cecil.dll')
$targets = @(
    'Game.Net.Lane', 'Game.Net.Curve', 'Game.Net.LaneOverlap', 'Game.Net.OverlapFlags',
    'Game.Net.LaneReservation', 'Game.Net.ReservationData', 'Game.Net.LaneObject',
    'Game.Net.CarLane', 'Game.Net.CarLaneFlags', 'Game.Vehicles.CarNavigation',
    'Game.Vehicles.CarCurrentLane', 'Game.Vehicles.CarNavigationLane',
    'Game.Pathfind.PathElement', 'Game.Pathfind.PathOwner',
    'Game.Simulation.CarLaneSpeedIterator', 'Game.Simulation.CarNavigationSystem',
    'Game.Net.LaneOverlapSystem', 'Game.Simulation.NetLaneReservationSystem',
    'Game.Simulation.PathfindSetupSystem', 'Game.Pathfind.PathfindSetupSystem',
    'Game.Prefabs.NetPieceLanes', 'Game.Prefabs.NetLaneInfo',
    'Game.Prefabs.NetSectionPrefab', 'Game.Prefabs.NetPiecePrefab', 'Game.Prefabs.PrefabSystem',
    'RoadBuilder.Systems.RoadBuilderNetSectionsSystem',
    'RoadBuilder.Systems.RoadBuilderGenerationDataSystem',
    'RoadBuilder.Domain.Components.Prefabs.RoadBuilderLaneInfo'
)
$assemblies = @((Join-Path $ManagedPath 'Game.dll'), (Join-Path $ManagedPath 'Unity.Entities.dll'))
if ($RoadBuilderAssembly) { $assemblies += $RoadBuilderAssembly }
foreach ($path in $assemblies) {
    $assembly = [Colossal.Mono.Cecil.AssemblyDefinition]::ReadAssembly($path)
    try {
        "ASSEMBLY $($assembly.Name.FullName)"
        "MVID $($assembly.MainModule.Mvid)"
        "SHA256 $((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash)"
        foreach ($type in $assembly.MainModule.Types | Where-Object FullName -in $targets | Sort-Object FullName) {
            "TYPE $($type.FullName)"
            foreach ($field in $type.Fields | Sort-Object Name) { "  FIELD $($field.FullName)" }
            foreach ($method in $type.Methods | Where-Object {
                $_.Name -match '^(Check|UpdateMaxSpeed|OnUpdate|GetPriority|AddPrefab|TryGetEntity)' -or
                $_.Name -in @('get_NetSections', 'get_RoadGenerationData', 'RefreshCustomContent')
            } | Sort-Object FullName) { "  METHOD $($method.FullName)" }
        }
    } finally { $assembly.Dispose() }
}
