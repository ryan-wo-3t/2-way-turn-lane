using Game.Net;
using Game.Pathfind;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Twtl.Mod.Diagnostics
{
    /// <summary>
    /// One-shot native pathfinder probe for a selected direct two-way car lane.
    /// It asks whether the graph permits travel from one end of that same lane
    /// to the other. It does not create a vehicle or modify the network.
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
            foreach (Entity lane in lanes)
            {
                if (probed >= 2) break;
                if (!manager.Exists(lane) || !manager.HasComponent<Game.Net.CarLane>(lane) ||
                    manager.HasComponent<SlaveLane>(lane)) continue;
                var car = manager.GetComponentData<Game.Net.CarLane>(lane);
                if ((car.m_Flags & CarLaneFlags.Twoway) == 0 || !graph.GetEdge(lane, out var edge)) continue;
                var flags = graph.GetFlags(edge);
                if ((flags & (EdgeFlags.Forward | EdgeFlags.Backward)) !=
                    (EdgeFlags.Forward | EdgeFlags.Backward)) continue;

                probed++;
                Probe(graph, lane, 0f, 1f);
                Probe(graph, lane, 1f, 0f);
            }
            Mod.Log.Info($"[PATH-PROBE] directTwoWayLanesProbed={probed} maxLanes=2; same-lane endpoint queries only, turn-only routing and safe entry UNKNOWN");
        }

        private static void Probe(NativePathfindData graph, Entity lane, float start, float end)
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
                action.m_StartTargets[0] = new PathTarget(lane, lane, start, 0f);
                action.m_EndTargets[0] = new PathTarget(lane, lane, end, 0f);
                PathfindJobs.PathfindJob.Execute(graph, Allocator.Temp,
                    new Unity.Mathematics.Random(1u), default(PathfindHeuristicData), 30f, 30f, ref action);
                var result = action.m_Result[0];
                bool traversedLane = false;
                for (int i = 0; i < action.m_Path.Length; i++)
                    if (action.m_Path[i].m_Target == lane) traversedLane = true;
                bool found = result.m_PathLength > 0 && result.m_Distance > 0f &&
                    result.m_ErrorCode == 0 && traversedLane;
                Mod.Log.Info($"[PATH-PROBE] lane={lane} start={start} end={end} found={found} traversedSelectedLane={traversedLane} pathLength={result.m_PathLength} pathEntries={action.m_Path.Length} distance={result.m_Distance} error={result.m_ErrorCode}; graph path only, no actual vehicle or eligible turn");
            }
            finally { action.Dispose(); }
        }
    }
}
