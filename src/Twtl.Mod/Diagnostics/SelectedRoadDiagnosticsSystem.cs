using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Game;
using Game.City;
using Game.Net;
using Game.Simulation;
using Game.UI.InGame;
using Unity.Entities;

namespace Twtl.Mod.Diagnostics
{
    /// <summary>Explicit one-shot read-only capture, bounded to the selected road's first 64 lanes.</summary>
    public partial class SelectedRoadDiagnosticsSystem : GameSystemBase
    {
        protected override void OnUpdate()
        {
            var settings = Mod.Options;
            if (settings?.CaptureSelectedRoad != true) return;
            settings.CaptureSelectedRoad = false;
            var timer = Stopwatch.StartNew();
            try
            {
                Entity selected = World.GetOrCreateSystemManaged<SelectedInfoUISystem>().selectedEntity;
                if (selected == Entity.Null || !EntityManager.Exists(selected) || !EntityManager.HasBuffer<SubLane>(selected))
                { Mod.Log.Warn("[CAPTURE] Select a road segment with SubLane data first."); return; }
                bool leftHand = World.GetOrCreateSystemManaged<CityConfigurationSystem>().leftHandTraffic;
                uint frame = World.GetOrCreateSystemManaged<SimulationSystem>().frameIndex;
                Mod.Log.Info($"[CAPTURE] owner={selected} frame={frame} leftHandTraffic={leftHand}");
                // EntityManager read APIs complete conflicting tracked jobs before accessing buffers.
                // Captures may stall simulation and are never performed continuously.
                var lanes = EntityManager.GetBuffer<SubLane>(selected, true);
                int count = Math.Min(lanes.Length, 64);
                // Copy identities before any subsequent EntityManager calls invalidate buffer handles.
                var identities = new Entity[count];
                for (int i = 0; i < count; i++) identities[i] = lanes[i].m_SubLane;
                Mod.Log.Info($"[CAPTURE] lanes={lanes.Length} captured={count}");
                CorridorTopologyDiagnostics.Write(EntityManager, identities);
                int vehicleBudget = 32;
                foreach (Entity lane in identities)
                {
                    if (!EntityManager.Exists(lane)) { Mod.Log.Warn("[CAPTURE] stale lane=" + lane); continue; }
                    Mod.Log.Info("[LANE] " + lane);
                    if (EntityManager.HasComponent<Lane>(lane))
                        Dump("laneNodes", EntityManager.GetComponentData<Lane>(lane));
                    if (EntityManager.HasComponent<EdgeLane>(lane))
                        Dump("edgeLane", EntityManager.GetComponentData<EdgeLane>(lane));
                    if (EntityManager.HasComponent<NodeLane>(lane))
                        Dump("nodeLane", EntityManager.GetComponentData<NodeLane>(lane));
                    if (EntityManager.HasComponent<Curve>(lane))
                        Dump("curve", EntityManager.GetComponentData<Curve>(lane));
                    if (EntityManager.HasComponent<Game.Net.CarLane>(lane))
                        Dump("carLane", EntityManager.GetComponentData<Game.Net.CarLane>(lane));
                    if (EntityManager.HasComponent<LaneReservation>(lane))
                        Dump("nativeReservation", EntityManager.GetComponentData<LaneReservation>(lane));
                    if (EntityManager.HasBuffer<LaneOverlap>(lane))
                    {
                        var overlaps = EntityManager.GetBuffer<LaneOverlap>(lane, true);
                        Mod.Log.Info("[LANE] overlapCount=" + overlaps.Length);
                        for (int i = 0; i < Math.Min(overlaps.Length, 64); i++) Dump("overlap", overlaps[i]);
                    }
                    if (EntityManager.HasBuffer<LaneObject>(lane))
                    {
                        var occupants = EntityManager.GetBuffer<LaneObject>(lane, true);
                        Mod.Log.Info("[LANE] objectCount=" + occupants.Length);
                        var objects = new LaneObject[Math.Min(occupants.Length, 64)];
                        for (int i = 0; i < objects.Length; i++) objects[i] = occupants[i];
                        foreach (var item in objects)
                        {
                            Dump("laneObject", item);
                            Entity vehicle = item.m_LaneObject;
                            if (vehicleBudget <= 0 || !EntityManager.Exists(vehicle)) continue;
                            vehicleBudget--;
                            Mod.Log.Info("[VEHICLE] " + vehicle);
                            if (EntityManager.HasComponent<Game.Vehicles.CarNavigation>(vehicle))
                                Dump("carNavigation", EntityManager.GetComponentData<Game.Vehicles.CarNavigation>(vehicle));
                            if (EntityManager.HasComponent<Game.Vehicles.CarCurrentLane>(vehicle))
                                Dump("carCurrentLane", EntityManager.GetComponentData<Game.Vehicles.CarCurrentLane>(vehicle));
                            if (EntityManager.HasComponent<Game.Objects.Moving>(vehicle))
                                Dump("moving", EntityManager.GetComponentData<Game.Objects.Moving>(vehicle));
                            if (EntityManager.HasComponent<Game.Objects.Transform>(vehicle))
                                Dump("transform", EntityManager.GetComponentData<Game.Objects.Transform>(vehicle));
                            if (EntityManager.HasComponent<Game.Vehicles.Blocker>(vehicle))
                                Dump("nativeBlocker", EntityManager.GetComponentData<Game.Vehicles.Blocker>(vehicle));
                            if (EntityManager.HasBuffer<Game.Vehicles.CarNavigationLane>(vehicle))
                            {
                                var navigation = EntityManager.GetBuffer<Game.Vehicles.CarNavigationLane>(vehicle, true);
                                Mod.Log.Info($"[VEHICLE] navigationLanes={navigation.Length} captured={Math.Min(navigation.Length, 16)}");
                                for (int i = 0; i < Math.Min(navigation.Length, 16); i++) Dump("navigationLane", navigation[i]);
                            }
                            if (EntityManager.HasBuffer<Game.Pathfind.PathElement>(vehicle))
                            {
                                int first = 0;
                                if (EntityManager.HasComponent<Game.Pathfind.PathOwner>(vehicle))
                                {
                                    var owner = EntityManager.GetComponentData<Game.Pathfind.PathOwner>(vehicle);
                                    Dump("pathOwner", owner);
                                    first = owner.m_ElementIndex;
                                }
                                var path = EntityManager.GetBuffer<Game.Pathfind.PathElement>(vehicle, true);
                                int remaining = first >= 0 && first <= path.Length ? path.Length - first : 0;
                                Mod.Log.Info($"[VEHICLE] pathElements={path.Length} first={first} captured={Math.Min(remaining, 16)}; raw route data, turn intent UNKNOWN");
                                for (int i = 0; i < Math.Min(remaining, 16); i++) Dump("pathElement", path[first + i]);
                            }
                        }
                    }
                }
            }
            catch (Exception error) { Mod.Log.Error(error, "Read-only lane capture failed"); }
            finally { Mod.Log.Info($"[CAPTURE] elapsedMs={timer.Elapsed.TotalMilliseconds:F3}; this is diagnostic capture time, not traffic-system overhead."); }
        }

        private static void Dump<T>(string label, T data) where T : struct
        {
            string fields = string.Join("; ", typeof(T).GetFields(BindingFlags.Instance | BindingFlags.Public)
                .OrderBy(x => x.Name).Select(field => field.Name + "=" + Format(field.GetValue(data), 0)));
            Mod.Log.Info("[DATA] " + label + " " + fields);
        }

        private static string Format(object? value, int depth)
        {
            if (value == null) return "null";
            Type type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || value is Entity || value is string || depth >= 3)
                return value.ToString() ?? "";
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
            return fields.Length == 0 ? value.ToString() ?? "" : "{" + string.Join(",", fields.Take(16)
                .Select(field => field.Name + "=" + Format(field.GetValue(value), depth + 1))) + "}";
        }
    }
}
