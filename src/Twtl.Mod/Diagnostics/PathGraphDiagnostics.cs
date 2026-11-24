using Game.Common;
using Game.Net;
using Game.Pathfind;
using Game.Prefabs;
using Unity.Collections;
using Unity.Entities;

namespace Twtl.Mod.Diagnostics
{
    internal static class PathGraphDiagnostics
    {
        // Global one-shot check: a virtual fixture may have no selectable road
        // surface. This never changes graph or lane data.
        public static void WriteCandidateBlockages(World world, EntityManager manager)
        {
            var queue = world.GetExistingSystemManaged<PathfindQueueSystem>();
            if (queue == null)
            {
                Mod.Log.Warn("[PATH-GRAPH-ALL] PathfindQueueSystem unavailable; blockage UNKNOWN.");
                return;
            }
            var graph = queue.GetDataContainer(out var writeHandle);
            writeHandle.Complete();
            var graphData = graph.GetReadOnlyData();
            int candidates = 0;
            int matchedPrimary = 0;
            int matchedSecondary = 0;
            int mismatches = 0;
            using (var query = manager.CreateEntityQuery(ComponentType.ReadOnly<Game.Net.CarLane>(),
                ComponentType.ReadOnly<EdgeLane>()))
            using (var lanes = query.ToEntityArray(Allocator.Temp))
            {
                foreach (Entity lane in lanes)
                {
                    var car = manager.GetComponentData<Game.Net.CarLane>(lane);
                    bool blocked = car.m_BlockageStart == 0 && car.m_BlockageEnd == byte.MaxValue;
                    bool twoway = (car.m_Flags & CarLaneFlags.Twoway) != 0;
                    if (!blocked && !twoway) continue;
                    candidates++;
                    bool primaryPresent = graph.GetEdge(lane, out var primary);
                    bool secondaryPresent = graph.GetSecondaryEdge(lane, out var secondary);
                    bool primaryBlocked = false;
                    bool secondaryBlocked = false;
                    if (primaryPresent)
                    {
                        var spec = graphData.GetEdge(primary).m_Specification;
                        primaryBlocked = (spec.m_Rules & RuleFlags.HasBlockage) != 0 &&
                            spec.m_BlockageStart == 0 && spec.m_BlockageEnd == byte.MaxValue;
                        if (primaryBlocked) matchedPrimary++;
                        if (blocked != primaryBlocked) mismatches++;
                    }
                    if (secondaryPresent)
                    {
                        var spec = graphData.GetEdge(secondary).m_Specification;
                        secondaryBlocked = (spec.m_Rules & RuleFlags.HasBlockage) != 0 &&
                            spec.m_BlockageStart == 0 && spec.m_BlockageEnd == byte.MaxValue;
                        if (secondaryBlocked) matchedSecondary++;
                        if (blocked != secondaryBlocked) mismatches++;
                    }
                    if (candidates <= 32)
                    {
                        Entity owner = manager.HasComponent<Owner>(lane)
                            ? manager.GetComponentData<Owner>(lane).m_Owner : Entity.Null;
                        Mod.Log.Info($"[PATH-GRAPH-ALL] lane={lane} owner={owner} twoway={twoway} fullLaneBlockage={blocked} primaryPresent={primaryPresent} primaryFullyBlocked={primaryBlocked} secondaryPresent={secondaryPresent} secondaryFullyBlocked={secondaryBlocked}");
                    }
                }
            }
            Mod.Log.Info($"[PATH-GRAPH-ALL] candidates={candidates} fullyBlockedPrimaryEdges={matchedPrimary} fullyBlockedSecondaryEdges={matchedSecondary} mismatches={mismatches} truncated={candidates > 32}; route rejection and vehicle safety UNKNOWN");
        }

        // One-shot selected-owner inspection. Graph flags are necessary evidence,
        // but they do not prove a completed route or safe vehicle behaviour.
        public static void Write(World world, EntityManager manager, Entity[] lanes)
        {
            var queue = world.GetExistingSystemManaged<PathfindQueueSystem>();
            if (queue == null)
            {
                Mod.Log.Warn("[PATH-GRAPH] PathfindQueueSystem unavailable; graph flags UNKNOWN.");
                return;
            }

            var graph = queue.GetDataContainer(out var writeHandle);
            writeHandle.Complete();
            var graphData = graph.GetReadOnlyData();
            int inspected = 0;
            int primary = 0;
            int both = 0;
            int slaveCount = 0;
            int twoWaySlaveCount = 0;
            int fullyBlockedLanes = 0;
            int fullyBlockedEdges = 0;
            int fullyBlockedSecondaryEdges = 0;
            int blockageMismatch = 0;
            foreach (Entity lane in lanes)
            {
                if (!manager.Exists(lane) || !manager.HasComponent<Game.Net.CarLane>(lane)) continue;
                inspected++;
                var car = manager.GetComponentData<Game.Net.CarLane>(lane);
                bool laneBlocked = car.m_BlockageStart == 0 && car.m_BlockageEnd == byte.MaxValue;
                if (laneBlocked) fullyBlockedLanes++;
                bool twoway = (car.m_Flags & CarLaneFlags.Twoway) != 0;
                bool slave = manager.HasComponent<SlaveLane>(lane);
                if (manager.HasComponent<MasterLane>(lane))
                {
                    var masterData = manager.GetComponentData<MasterLane>(lane);
                    Mod.Log.Info($"[PATH-GRAPH] lane={lane} masterGroup={masterData.m_Group} masterFlags={masterData.m_Flags} minIndex={masterData.m_MinIndex} maxIndex={masterData.m_MaxIndex}");
                }
                if (slave)
                {
                    slaveCount++;
                    if (twoway) twoWaySlaveCount++;
                    var slaveData = manager.GetComponentData<SlaveLane>(lane);
                    Mod.Log.Info($"[PATH-GRAPH] lane={lane} slaveGroup={slaveData.m_Group} slaveFlags={slaveData.m_Flags} minIndex={slaveData.m_MinIndex} maxIndex={slaveData.m_MaxIndex} subIndex={slaveData.m_SubIndex} masterIndex={slaveData.m_MasterIndex}");
                }
                if (manager.HasComponent<PrefabRef>(lane))
                {
                    Entity prefab = manager.GetComponentData<PrefabRef>(lane).m_Prefab;
                    if (manager.Exists(prefab) && manager.HasComponent<CarLaneData>(prefab))
                    {
                        var carData = manager.GetComponentData<CarLaneData>(prefab);
                        var methods = Game.Vehicles.VehicleUtils.GetPathMethods(car, carData);
                        Entity pathfindPrefab = Entity.Null;
                        bool hasPathfindCarData = false;
                        if (manager.HasComponent<NetLaneData>(prefab))
                        {
                            pathfindPrefab = manager.GetComponentData<NetLaneData>(prefab).m_PathfindPrefab;
                            hasPathfindCarData = manager.Exists(pathfindPrefab) && manager.HasComponent<PathfindCarData>(pathfindPrefab);
                        }
                        Mod.Log.Info($"[PATH-GRAPH] lane={lane} prefab={prefab} maxSize={carData.m_MaxSize} roadTypes={carData.m_RoadTypes} pathMethods={methods} pathfindPrefab={pathfindPrefab} hasPathfindCarData={hasPathfindCarData} master={manager.HasComponent<MasterLane>(lane)} slave={slave}");
                    }
                    else
                        Mod.Log.Info($"[PATH-GRAPH] lane={lane} prefab={prefab} carLaneData=missing");
                }
                if (graph.GetEdge(lane, out var edge))
                {
                    primary++;
                    EdgeFlags flags = graph.GetFlags(edge);
                    var spec = graphData.GetEdge(edge).m_Specification;
                    bool graphBlocked = (spec.m_Rules & RuleFlags.HasBlockage) != 0 &&
                        spec.m_BlockageStart == 0 && spec.m_BlockageEnd == byte.MaxValue;
                    if (graphBlocked) fullyBlockedEdges++;
                    if (laneBlocked != graphBlocked) blockageMismatch++;
                    bool forward = (flags & EdgeFlags.Forward) != 0;
                    bool backward = (flags & EdgeFlags.Backward) != 0;
                    if (twoway && forward && backward) both++;
                    Mod.Log.Info($"[PATH-GRAPH] lane={lane} twoway={twoway} edgeIndex={edge.m_Index} flags={flags} forward={forward} backward={backward}");
                    if (twoway || laneBlocked || graphBlocked)
                        Mod.Log.Info($"[PATH-GRAPH] lane={lane} laneBlockage={car.m_BlockageStart}..{car.m_BlockageEnd} graphHasBlockage={(spec.m_Rules & RuleFlags.HasBlockage) != 0} graphBlockage={spec.m_BlockageStart}..{spec.m_BlockageEnd} fullBlockageMatches={laneBlocked && graphBlocked}");
                }
                else
                    Mod.Log.Info($"[PATH-GRAPH] lane={lane} twoway={twoway} primaryEdge=missing slave={slave}; installed LanesModifiedSystem skips SlaveLane chunks, so a missing direct edge is expected for a slave; routing via its master and endpoints UNKNOWN");

                if (graph.GetSecondaryEdge(lane, out var secondary))
                {
                    EdgeFlags flags = graph.GetFlags(secondary);
                    var spec = graphData.GetEdge(secondary).m_Specification;
                    bool graphBlocked = (spec.m_Rules & RuleFlags.HasBlockage) != 0 &&
                        spec.m_BlockageStart == 0 && spec.m_BlockageEnd == byte.MaxValue;
                    if (graphBlocked) fullyBlockedSecondaryEdges++;
                    if (laneBlocked != graphBlocked) blockageMismatch++;
                    Mod.Log.Info($"[PATH-GRAPH] lane={lane} secondaryEdgeIndex={secondary.m_Index} flags={flags}");
                    if (twoway || laneBlocked || graphBlocked)
                        Mod.Log.Info($"[PATH-GRAPH] lane={lane} secondaryLaneBlockage={car.m_BlockageStart}..{car.m_BlockageEnd} secondaryGraphHasBlockage={(spec.m_Rules & RuleFlags.HasBlockage) != 0} secondaryGraphBlockage={spec.m_BlockageStart}..{spec.m_BlockageEnd} fullBlockageMatches={laneBlocked && graphBlocked}");
                }
            }
            Mod.Log.Info($"[PATH-GRAPH] inspectedCarLanes={inspected} slaveLanes={slaveCount} twoWaySlaves={twoWaySlaveCount} primaryEdges={primary} twoWayEdgesWithBothDirectionFlags={both} fullyBlockedLanes={fullyBlockedLanes} fullyBlockedPrimaryEdges={fullyBlockedEdges} fullyBlockedSecondaryEdges={fullyBlockedSecondaryEdges} blockageMismatch={blockageMismatch}; endpoint routing and traffic safety UNKNOWN");
        }
    }
}
