using System;
using System.Collections.Generic;
using Game.Net;
using Game.Pathfind;
using Unity.Entities;

namespace Twtl.Mod.Diagnostics
{
    internal static class JunctionConnectionDiagnostics
    {
        // Selected edge and its two endpoint nodes only. An isolated junction
        // may reveal candidate turns, never permission to admit a vehicle.
        private const int MaximumNodeLanes = 64;

        public static void Write(World world, EntityManager manager, Entity edgeEntity, Entity[] edgeLanes)
        {
            if (!manager.HasComponent<Game.Net.Edge>(edgeEntity))
            {
                Mod.Log.Info("[JUNCTION] selected owner has no Edge component");
                return;
            }

            var edge = manager.GetComponentData<Game.Net.Edge>(edgeEntity);
            var centers = new List<Lane>();
            int centerNoPassing = 0;
            int centerMissingOverlaps = 0;
            foreach (var laneEntity in edgeLanes)
            {
                if (!manager.Exists(laneEntity) || !manager.HasComponent<Lane>(laneEntity) ||
                    !manager.HasComponent<Game.Net.CarLane>(laneEntity)) continue;
                var car = manager.GetComponentData<Game.Net.CarLane>(laneEntity);
                if ((car.m_Flags & CarLaneFlags.Twoway) != 0)
                {
                    centers.Add(manager.GetComponentData<Lane>(laneEntity));
                    if ((car.m_Flags & CarLaneFlags.ForbidPassing) != 0) centerNoPassing++;
                    if (!manager.HasBuffer<LaneOverlap>(laneEntity) ||
                        manager.GetBuffer<LaneOverlap>(laneEntity, true).Length == 0) centerMissingOverlaps++;
                }
            }
            Mod.Log.Info($"[JUNCTION] selectedCenterLanes={centers.Count} forbidPassing={centerNoPassing} withoutOverlapRecords={centerMissingOverlaps}; no traffic safety inference");

            var queue = world.GetExistingSystemManaged<PathfindQueueSystem>();
            NativePathfindData graph = default;
            bool hasGraph = queue != null;
            if (queue != null)
            {
                graph = queue.GetDataContainer(out var writeHandle);
                writeHandle.Complete();
            }
            InspectNode("start", edge.m_Start);
            if (edge.m_End != edge.m_Start) InspectNode("end", edge.m_End);

            void InspectNode(string side, Entity node)
            {
                if (!manager.Exists(node) || !manager.HasBuffer<SubLane>(node))
                {
                    Mod.Log.Info($"[JUNCTION] side={side} node={node} subLanes=missing");
                    return;
                }
                var buffer = manager.GetBuffer<SubLane>(node, true);
                int captured = Math.Min(buffer.Length, MaximumNodeLanes);
                var identities = new Entity[captured];
                for (int i = 0; i < captured; i++) identities[i] = buffer[i].m_SubLane;
                int carCount = 0;
                int centerEndpointMatches = 0;
                int centerUTurnFlags = 0;
                int centerUnsafeFlags = 0;
                int centerTwowayFlags = 0;
                foreach (Entity laneEntity in identities)
                {
                    if (!manager.Exists(laneEntity) || !manager.HasComponent<Lane>(laneEntity) ||
                        !manager.HasComponent<Game.Net.CarLane>(laneEntity)) continue;
                    carCount++;
                    var lane = manager.GetComponentData<Lane>(laneEntity);
                    var car = manager.GetComponentData<Game.Net.CarLane>(laneEntity);
                    bool centerStart = false;
                    bool centerEnd = false;
                    foreach (var center in centers)
                    {
                        centerStart |= lane.m_StartNode.Equals(center.m_StartNode) ||
                                       lane.m_EndNode.Equals(center.m_StartNode);
                        centerEnd |= lane.m_StartNode.Equals(center.m_EndNode) ||
                                     lane.m_EndNode.Equals(center.m_EndNode);
                    }
                    if (centerStart || centerEnd)
                    {
                        centerEndpointMatches++;
                        if ((car.m_Flags & CarLaneFlags.UTurnLeft) != 0) centerUTurnFlags++;
                        if ((car.m_Flags & CarLaneFlags.Unsafe) != 0) centerUnsafeFlags++;
                        if ((car.m_Flags & CarLaneFlags.Twoway) != 0) centerTwowayFlags++;
                    }
                    string graphFlags = "unavailable";
                    if (hasGraph)
                        graphFlags = graph.GetEdge(laneEntity, out var id)
                            ? graph.GetFlags(id).ToString() : "no-primary-edge";
                    Mod.Log.Info($"[JUNCTION] side={side} node={node} lane={laneEntity} nodeLane={manager.HasComponent<NodeLane>(laneEntity)} carFlags={car.m_Flags} graphFlags={graphFlags} matchesCenterStart={centerStart} matchesCenterEnd={centerEnd} start={Describe(lane.m_StartNode)} end={Describe(lane.m_EndNode)}");
                }
                Mod.Log.Info($"[JUNCTION] side={side} node={node} subLanes={buffer.Length} captured={captured} carLanes={carCount} exactCenterEndpointMatches={centerEndpointMatches} centerMatchesWithUTurnFlag={centerUTurnFlags} centerMatchesWithUnsafeFlag={centerUnsafeFlags} centerMatchesWithTwowayFlag={centerTwowayFlags}; path-node equality and flags are not a route or safety test");
            }
        }

        private static string Describe(PathNode node) =>
            $"{node.GetOwnerIndex()}:{node.GetLaneIndex()}:{node.GetCurvePos():F3}:{node.IsSecondary()}";
    }
}
