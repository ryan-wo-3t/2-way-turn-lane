using Game.Net;
using Game.Pathfind;
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
            foreach (Entity lane in lanes)
            {
                if (!manager.Exists(lane) || !manager.HasComponent<Game.Net.CarLane>(lane)) continue;
                inspected++;
                var car = manager.GetComponentData<Game.Net.CarLane>(lane);
                bool twoway = (car.m_Flags & CarLaneFlags.Twoway) != 0;
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
                    Mod.Log.Info($"[PATH-GRAPH] lane={lane} twoway={twoway} primaryEdge=missing; graph reachability UNKNOWN");

                if (graph.GetSecondaryEdge(lane, out var secondary))
                {
                    EdgeFlags flags = graph.GetFlags(secondary);
                    Mod.Log.Info($"[PATH-GRAPH] lane={lane} secondaryEdgeIndex={secondary.m_Index} flags={flags}");
                }
            }
            Mod.Log.Info($"[PATH-GRAPH] inspectedCarLanes={inspected} primaryEdges={primary} twoWayEdgesWithBothDirectionFlags={both}; endpoint routing and traffic safety UNKNOWN");
        }
    }
}
