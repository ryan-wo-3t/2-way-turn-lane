using Game.Net;
using Game.Pathfind;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Twtl.Mod.Diagnostics
{
    /// <summary>
    /// One-shot native pathfinder probe for selected direct two-way car lanes.
    /// It checks each lane and, when their path nodes touch, travel across the
    /// pair. It does not create a vehicle or modify the network.
    /// </summary>
    internal static class SyntheticPathDiagnostics
    {
        public static void Write(World world, EntityManager manager, Entity[] lanes)
        {
            var queue = world.GetExistingSystemManaged<PathfindQueueSystem>();
            if (queue == null)
            {
                Mod.Log.Warn("[PATH-PROBE] PathfindQueueSystem unavailable; native route result UNKNOWN.");
                return;
            }

            var graph = queue.GetDataContainer(out var writeHandle);
            writeHandle.Complete();
            int probed = 0;
            var direct = new Entity[2];
            var nodes = new Lane[2];
            foreach (Entity lane in lanes)
            {
                if (probed >= 2) break;
                if (!manager.Exists(lane) || !manager.HasComponent<Lane>(lane) ||
                    !manager.HasComponent<Game.Net.CarLane>(lane) ||
                    manager.HasComponent<SlaveLane>(lane)) continue;
                var car = manager.GetComponentData<Game.Net.CarLane>(lane);
                if ((car.m_Flags & CarLaneFlags.Twoway) == 0 || !graph.GetEdge(lane, out var edge)) continue;
                var flags = graph.GetFlags(edge);
                if ((flags & (EdgeFlags.Forward | EdgeFlags.Backward)) !=
                    (EdgeFlags.Forward | EdgeFlags.Backward)) continue;

                direct[probed] = lane;
                nodes[probed] = manager.GetComponentData<Lane>(lane);
                probed++;
                Probe(graph, lane, 0f, lane, 1f);
                Probe(graph, lane, 1f, lane, 0f);
            }
            int joinedQueries = 0;
            if (probed == 2)
            {
                Entity first = direct[0];
                Entity second = direct[1];
                if (nodes[0].m_EndNode.Equals(nodes[1].m_StartNode))
                {
                    Probe(graph, first, 0f, second, 1f);
                    Probe(graph, second, 1f, first, 0f);
                    joinedQueries = 2;
                }
                else if (nodes[1].m_EndNode.Equals(nodes[0].m_StartNode))
                {
                    Probe(graph, second, 0f, first, 1f);
                    Probe(graph, first, 1f, second, 0f);
                    joinedQueries = 2;
                }
            }
            Mod.Log.Info($"[PATH-PROBE] directTwoWayLanesProbed={probed} joinedQueries={joinedQueries} maxQueries=6; turn-only routing, roadside entry, and traffic safety UNKNOWN");
        }

        private static void Probe(NativePathfindData graph, Entity startLane, float start,
            Entity endLane, float end)
        {
            var parameters = new PathfindParameters
            {
                m_Methods = PathMethod.Road | PathMethod.MediumRoad,
                m_PathfindFlags = PathfindFlags.Stable | PathfindFlags.NoHeuristics,
                m_Weights = new PathfindWeights(1f, 1f, 1f, 1f),
                m_MaxSpeed = new float2(30f, 30f),
                m_WalkSpeed = new float2(1f, 1f),
                // This probe is only for one selected lane. Bound an unexpected
                // graph search instead of exploring an entire connected city.
                m_MaxCost = 1000f,
                m_MaxResultCount = 1
            };
            var action = new PathfindActionData(1, 1, Allocator.Temp,
                parameters, SetupTargetType.CurrentLocation, SetupTargetType.CurrentLocation);
            try
            {
                action.m_StartTargets[0] = new PathTarget(startLane, startLane, start, 0f);
                action.m_EndTargets[0] = new PathTarget(endLane, endLane, end, 0f);
                PathfindJobs.PathfindJob.Execute(graph, Allocator.Temp,
                    new Unity.Mathematics.Random(1u), default(PathfindHeuristicData), 30f, 30f, ref action);
                var result = action.m_Result[0];
                bool traversedStart = false;
                bool traversedEnd = false;
                for (int i = 0; i < action.m_Path.Length; i++)
                {
                    if (action.m_Path[i].m_Target == startLane) traversedStart = true;
                    if (action.m_Path[i].m_Target == endLane) traversedEnd = true;
                }
                bool found = result.m_PathLength > 0 && result.m_Distance > 0f &&
                    result.m_ErrorCode == 0 && traversedStart && traversedEnd;
                Mod.Log.Info($"[PATH-PROBE] startLane={startLane} start={start} endLane={endLane} end={end} found={found} traversedStart={traversedStart} traversedEnd={traversedEnd} pathLength={result.m_PathLength} pathEntries={action.m_Path.Length} distance={result.m_Distance} error={result.m_ErrorCode}; graph path only, no actual vehicle or eligible turn");
            }
            finally { action.Dispose(); }
        }
    }
}
