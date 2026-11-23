using Game.Net;
using Game.Pathfind;
using Game.Prefabs;
using Unity.Entities;

namespace Twtl.Mod.Diagnostics
{
    internal static class PathGraphDiagnostics
    {
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
            int inspected = 0;
            int primary = 0;
            int both = 0;
            int slaveCount = 0;
            int twoWaySlaveCount = 0;
            foreach (Entity lane in lanes)
            {
                if (!manager.Exists(lane) || !manager.HasComponent<Game.Net.CarLane>(lane)) continue;
                inspected++;
                var car = manager.GetComponentData<Game.Net.CarLane>(lane);
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
                    bool forward = (flags & EdgeFlags.Forward) != 0;
                    bool backward = (flags & EdgeFlags.Backward) != 0;
                    if (twoway && forward && backward) both++;
                    Mod.Log.Info($"[PATH-GRAPH] lane={lane} twoway={twoway} edgeIndex={edge.m_Index} flags={flags} forward={forward} backward={backward}");
                }
                else
                    Mod.Log.Info($"[PATH-GRAPH] lane={lane} twoway={twoway} primaryEdge=missing slave={slave}; installed LanesModifiedSystem skips SlaveLane chunks, so a missing direct edge is expected for a slave; routing via its master and endpoints UNKNOWN");

                if (graph.GetSecondaryEdge(lane, out var secondary))
                {
                    EdgeFlags flags = graph.GetFlags(secondary);
                    Mod.Log.Info($"[PATH-GRAPH] lane={lane} secondaryEdgeIndex={secondary.m_Index} flags={flags}");
                }
            }
            Mod.Log.Info($"[PATH-GRAPH] inspectedCarLanes={inspected} slaveLanes={slaveCount} twoWaySlaves={twoWaySlaveCount} primaryEdges={primary} twoWayEdgesWithBothDirectionFlags={both}; endpoint routing and traffic safety UNKNOWN");
        }
    }
}
