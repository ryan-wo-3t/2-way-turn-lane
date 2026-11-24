using System;
using System.Text;
using Game.Common;
using Game.Net;
using Game.Vehicles;
using Unity.Collections;
using Unity.Entities;

namespace Twtl.Mod.Diagnostics
{
    /// <summary>
    /// One-shot observation of native upcoming movements. A turn flag alone is
    /// not permission to use a shared lane: junction and parking turns share it.
    /// </summary>
    internal static class TurnIntentDiagnostics
    {
        public static void Write(EntityManager manager)
        {
            int active = 0;
            int withNavigation = 0;
            int withTurn = 0;
            int roadNodeTurns = 0;
            int roadEdgeTurns = 0;
            int parkingTurns = 0;
            int connectionTurns = 0;
            int otherTurns = 0;
            int roundaboutTurns = 0;
            int turnThenConnection = 0;
            int buildingConnection = 0;
            int sampled = 0;
            using (var query = manager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<CarCurrentLane>(),
                    ComponentType.ReadOnly<CarNavigationLane>() },
                None = new[] { ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Game.Tools.Temp>() }
            }))
            using (var cars = query.ToEntityArray(Allocator.Temp))
            {
                for (int c = 0; c < cars.Length && active < 512; c++)
                {
                    Entity vehicle = cars[c];
                    if (manager.GetComponentData<CarCurrentLane>(vehicle).m_Lane == Entity.Null) continue;
                    active++;
                    var navigation = manager.GetBuffer<CarNavigationLane>(vehicle, true);
                    if (navigation.Length == 0) continue;
                    withNavigation++;
                    int limit = Math.Min(navigation.Length, 10);
                    int turnIndex = -1;
                    int connectionIndex = -1;
                    bool building = false;
                    for (int i = 0; i < limit; i++)
                    {
                        var flags = navigation[i].m_Flags;
                        if (turnIndex < 0 && (flags & (Game.Vehicles.CarLaneFlags.TurnLeft |
                            Game.Vehicles.CarLaneFlags.TurnRight)) != 0) turnIndex = i;
                        if (turnIndex < 0 || i < turnIndex || i > turnIndex + 3) continue;
                        Entity lane = navigation[i].m_Lane;
                        if (!manager.Exists(lane) ||
                            (!manager.HasComponent<ConnectionLane>(lane) &&
                             !manager.HasComponent<ParkingLane>(lane))) continue;
                        connectionIndex = i;
                        if (manager.HasComponent<Owner>(lane))
                        {
                            Entity owner = manager.GetComponentData<Owner>(lane).m_Owner;
                            building = manager.Exists(owner) && manager.HasComponent<Game.Buildings.Building>(owner);
                        }
                        break;
                    }
                    if (turnIndex >= 0)
                    {
                        withTurn++;
                        Entity turnLane = navigation[turnIndex].m_Lane;
                        if (!manager.Exists(turnLane)) otherTurns++;
                        else if (manager.HasComponent<ParkingLane>(turnLane)) parkingTurns++;
                        else if (manager.HasComponent<ConnectionLane>(turnLane)) connectionTurns++;
                        else if (manager.HasComponent<NodeLane>(turnLane)) roadNodeTurns++;
                        else if (manager.HasComponent<EdgeLane>(turnLane)) roadEdgeTurns++;
                        else otherTurns++;
                        if ((navigation[turnIndex].m_Flags & Game.Vehicles.CarLaneFlags.Roundabout) != 0)
                            roundaboutTurns++;
                    }
                    if (connectionIndex >= 0) turnThenConnection++;
                    if (building) buildingConnection++;
                    if (turnIndex < 0 || sampled >= 8) continue;
                    sampled++;
                    var sequence = new StringBuilder();
                    for (int i = 0; i < limit; i++)
                    {
                        if (i != 0) sequence.Append(" | ");
                        var item = navigation[i];
                        Entity lane = item.m_Lane;
                        string kind = !manager.Exists(lane) ? "stale" :
                            manager.HasComponent<ConnectionLane>(lane) ? "connection" :
                            manager.HasComponent<ParkingLane>(lane) ? "parking" :
                            manager.HasComponent<NodeLane>(lane) ? "node" :
                            manager.HasComponent<EdgeLane>(lane) ? "edge" : "other";
                        sequence.Append(i).Append(':').Append(lane).Append(':').Append(kind)
                            .Append(':').Append(item.m_Flags);
                    }
                    Mod.Log.Info($"[TURN-INTENT] vehicle={vehicle} turnIndex={turnIndex} connectionIndex={connectionIndex} buildingOwner={building} navigation={sequence}; observation only, entry authorization UNKNOWN");
                }
            }
            Mod.Log.Info($"[TURN-INTENT] scannedActiveCars={active} withNavigation={withNavigation} withTurn={withTurn} roadNodeTurns={roadNodeTurns} roadEdgeTurns={roadEdgeTurns} parkingTurns={parkingTurns} connectionTurns={connectionTurns} otherTurns={otherTurns} roundaboutTurns={roundaboutTurns} turnThenConnection={turnThenConnection} buildingConnection={buildingConnection} sampled={sampled} maxCars=512 maxSegments=10; no shared-lane admission or vehicle modification");
        }
    }
}
