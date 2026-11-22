using System;
using System.Collections.Generic;
using Game.Net;
using Game.Pathfind;
using Unity.Entities;
using Unity.Mathematics;

namespace Twtl.Mod.Diagnostics
{
    internal static class PathNodeContinuityDiagnostics
    {
        // Geometry is only a cross-check. Matching path nodes still do not prove that
        // the pathfinder accepts travel in both directions or enforces turn-only use.
        private const float EndpointToleranceMetres = 0.05f;

        public static void Write(EntityManager manager, Entity[] lanes)
        {
            var centers = new List<CenterLane>();
            foreach (Entity entity in lanes)
            {
                if (!manager.Exists(entity) || !manager.HasComponent<Lane>(entity) ||
                    !manager.HasComponent<Curve>(entity) || !manager.HasComponent<Game.Net.CarLane>(entity)) continue;
                var car = manager.GetComponentData<Game.Net.CarLane>(entity);
                if ((car.m_Flags & CarLaneFlags.Twoway) == 0) continue;
                var lane = manager.GetComponentData<Lane>(entity);
                var curve = manager.GetComponentData<Curve>(entity).m_Bezier;
                centers.Add(new CenterLane(entity, lane, curve.a, curve.d));
                Mod.Log.Info($"[PATH-NODES] lane={entity} start={Describe(lane.m_StartNode)} middle={Describe(lane.m_MiddleNode)} end={Describe(lane.m_EndNode)}");
            }

            int touchingPairs = 0;
            for (int i = 0; i < centers.Count; i++)
                for (int j = i + 1; j < centers.Count; j++)
                {
                    var a = centers[i];
                    var b = centers[j];
                    float endToStart = math.distance(a.EndPosition, b.StartPosition);
                    float startToEnd = math.distance(a.StartPosition, b.EndPosition);
                    if (endToStart <= EndpointToleranceMetres)
                    {
                        touchingPairs++;
                        WritePair(a.Entity, b.Entity, a.Lane.m_EndNode, b.Lane.m_StartNode, endToStart);
                    }
                    if (startToEnd <= EndpointToleranceMetres)
                    {
                        touchingPairs++;
                        WritePair(b.Entity, a.Entity, b.Lane.m_EndNode, a.Lane.m_StartNode, startToEnd);
                    }
                }
            Mod.Log.Info($"[PATH-NODES] twoWayLanes={centers.Count} touchingDirectedPairs={touchingPairs} toleranceMetres={EndpointToleranceMetres}; graph adjacency and bidirectional routing UNKNOWN");
        }

        private static void WritePair(Entity from, Entity to, PathNode end, PathNode start, float distance)
        {
            Mod.Log.Info($"[PATH-NODES] from={from} to={to} endpointDistanceMetres={distance:F4} exactNodeMatch={end.Equals(start)} sameOwner={end.OwnerEquals(start)} sameIgnoringCurvePos={end.EqualsIgnoreCurvePos(start)}; endpoint identity only");
        }

        private static string Describe(PathNode node) =>
            $"ownerIndex={node.GetOwnerIndex()},laneIndex={node.GetLaneIndex()},curvePos={node.GetCurvePos():F4},secondary={node.IsSecondary()}";

        private readonly struct CenterLane
        {
            public readonly Entity Entity;
            public readonly Lane Lane;
            public readonly float3 StartPosition;
            public readonly float3 EndPosition;

            public CenterLane(Entity entity, Lane lane, float3 startPosition, float3 endPosition)
            {
                Entity = entity;
                Lane = lane;
                StartPosition = startPosition;
                EndPosition = endPosition;
            }
        }
    }
}
