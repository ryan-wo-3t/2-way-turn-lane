using Game.Net;
using Game.Pathfind;
using Unity.Entities;

namespace Twtl.Mod.Diagnostics
{
    /// <summary>Describes native path targets without inferring a driver's final turn intent.</summary>
    internal static class RouteTargetDiagnostics
    {
        public static string Describe(EntityManager manager, PathElement element)
        {
            Entity target = element.m_Target;
            if (!manager.Exists(target)) return $"target={target} kind=stale";

            bool car = manager.HasComponent<Game.Net.CarLane>(target);
            bool node = manager.HasComponent<NodeLane>(target);
            bool edge = manager.HasComponent<EdgeLane>(target);
            string kind = car ? (node ? "car-node-lane" : edge ? "car-edge-lane" : "car-other-lane")
                              : node ? "non-car-node-lane" : edge ? "non-car-edge-lane" : "other";
            string direction = element.m_TargetDelta.y > element.m_TargetDelta.x ? "forward" :
                               element.m_TargetDelta.y < element.m_TargetDelta.x ? "backward" : "point";
            if (!car) return $"target={target} kind={kind} pathDirection={direction}";

            var flags = manager.GetComponentData<Game.Net.CarLane>(target).m_Flags;
            bool left = (flags & (CarLaneFlags.TurnLeft | CarLaneFlags.GentleTurnLeft)) != 0;
            bool right = (flags & (CarLaneFlags.TurnRight | CarLaneFlags.GentleTurnRight)) != 0;
            bool uturn = (flags & (CarLaneFlags.UTurnLeft | CarLaneFlags.UTurnRight)) != 0;
            return $"target={target} kind={kind} pathDirection={direction} nativeTurnLeft={left} nativeTurnRight={right} nativeUTurn={uturn} carFlags={flags}; native flags describe this path segment, not proven shared-lane intent";
        }
    }
}
