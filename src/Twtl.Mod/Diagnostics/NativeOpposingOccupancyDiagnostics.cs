using System;
using Game.Net;
using Game.Pathfind;
using Unity.Entities;

namespace Twtl.Mod.Diagnostics
{
    /// <summary>
    /// One-shot read-only evidence for the native same-lane oncoming check.
    /// Curve position order identifies travel direction in the installed
    /// CarLaneSpeedIterator; it does not prove that braking occurred.
    /// </summary>
    internal static class NativeOpposingOccupancyDiagnostics
    {
        private const int MaximumObjectsPerLane = 32;
        private const int MaximumDetailsPerLane = 8;

        public static void Write(EntityManager manager, Entity[] lanes)
        {
            int centerCount = 0;
            foreach (Entity laneEntity in lanes)
            {
                if (!manager.Exists(laneEntity) || !manager.HasComponent<Game.Net.CarLane>(laneEntity)) continue;
                var car = manager.GetComponentData<Game.Net.CarLane>(laneEntity);
                if ((car.m_Flags & CarLaneFlags.Twoway) == 0) continue;
                centerCount++;
                if (manager.HasComponent<SlaveLane>(laneEntity))
                {
                    Mod.Log.Info($"[ONCOMING] lane={laneEntity} is a slave; direct same-lane path use unverified");
                    continue;
                }
                if (!manager.HasBuffer<LaneObject>(laneEntity))
                {
                    Mod.Log.Info($"[ONCOMING] lane={laneEntity} LaneObject buffer missing; occupancy unknown");
                    continue;
                }

                var occupants = manager.GetBuffer<LaneObject>(laneEntity, true);
                int captured = Math.Min(occupants.Length, MaximumObjectsPerLane);
                var objects = new LaneObject[captured];
                for (int i = 0; i < captured; i++) objects[i] = occupants[i];
                int forward = 0;
                int backward = 0;
                int vehicles = 0;
                int other = 0;
                for (int i = 0; i < captured; i++)
                {
                    var item = objects[i];
                    bool reverse = item.m_CurvePosition.y < item.m_CurvePosition.x;
                    if (reverse) backward++; else forward++;
                    bool vehicle = manager.Exists(item.m_LaneObject) &&
                        manager.HasComponent<Game.Vehicles.CarCurrentLane>(item.m_LaneObject);
                    if (vehicle) vehicles++; else other++;
                    if (i < MaximumDetailsPerLane)
                        Mod.Log.Info($"[ONCOMING] lane={laneEntity} object={item.m_LaneObject} reverse={reverse} curveStart={item.m_CurvePosition.x:F3} curveEnd={item.m_CurvePosition.y:F3} carCurrentLane={vehicle}");
                }
                Mod.Log.Info($"[ONCOMING] lane={laneEntity} objects={occupants.Length} captured={captured} forward={forward} backward={backward} carCurrentLaneObjects={vehicles} otherObjects={other} bothDirectionsPresent={forward > 0 && backward > 0}; occupancy and direction evidence only, braking and collision outcome unknown");
            }
            Mod.Log.Info($"[ONCOMING] twoWayLanes={centerCount}; installed CarLaneSpeedIterator.CheckCurrentLane handles same-lane opposite direction, but a moving-car trial is required");
        }
    }
}
