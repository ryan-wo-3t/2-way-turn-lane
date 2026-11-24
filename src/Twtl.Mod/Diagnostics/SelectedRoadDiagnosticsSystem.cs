using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Game;
using Game.City;
using Game.Net;
using Game.Simulation;
using Game.UI.InGame;
using RoadBuilder.Systems.UI;
using Unity.Collections;
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
                string selectionSource = "SelectedInfoUISystem";
                if (selected == Entity.Null || !EntityManager.Exists(selected) || !EntityManager.HasBuffer<SubLane>(selected))
                {
                    // Road Builder's editor owns its selected edge independently of the
                    // game's info panel. Use that public selection only for this read-only
                    // one-shot capture; never use it to authorize traffic or mutate a road.
                    Entity working = World.GetExistingSystemManaged<RoadBuilderUISystem>()?.WorkingEntity ?? Entity.Null;
                    if (working != Entity.Null && EntityManager.Exists(working) && EntityManager.HasBuffer<SubLane>(working))
                    {
                        selected = working;
                        selectionSource = "RoadBuilderUISystem.WorkingEntity";
                    }
                }
                if (selected == Entity.Null || !EntityManager.Exists(selected) || !EntityManager.HasBuffer<SubLane>(selected))
                { Mod.Log.Warn("[CAPTURE] Select a road segment with SubLane data in the game info panel or Road Builder editor first."); return; }
                bool leftHand = World.GetOrCreateSystemManaged<CityConfigurationSystem>().leftHandTraffic;
                uint frame = World.GetOrCreateSystemManaged<SimulationSystem>().frameIndex;
                Mod.Log.Info($"[CAPTURE] source={selectionSource} owner={selected} frame={frame} leftHandTraffic={leftHand}");
                // EntityManager read APIs complete conflicting tracked jobs before accessing buffers.
                // Captures may stall simulation and are never performed continuously.
                var lanes = EntityManager.GetBuffer<SubLane>(selected, true);
                int count = Math.Min(lanes.Length, 64);
                // Copy identities before any subsequent EntityManager calls invalidate buffer handles.
                var identities = new Entity[count];
                for (int i = 0; i < count; i++) identities[i] = lanes[i].m_SubLane;
                Mod.Log.Info($"[CAPTURE] lanes={lanes.Length} captured={count}");
                try { RoadBuilderConfigurationDiagnostics.Write(World, EntityManager, selected); }
                catch (Exception error) { Mod.Log.Error(error, "Road Builder configuration capture failed; continuing native lane capture."); }
                try { NativeLanePrefabDiagnostics.Write(World); }
                catch (Exception error) { Mod.Log.Error(error, "Native two-way prefab catalogue capture failed; continuing native lane capture."); }
                CorridorTopologyDiagnostics.Write(EntityManager, identities);
                PathNodeContinuityDiagnostics.Write(EntityManager, identities);
                try { PathGraphDiagnostics.Write(World, EntityManager, identities); }
                catch (Exception error) { Mod.Log.Error(error, "Read-only path graph capture failed; continuing native lane capture."); }
                try { NativeOpposingOccupancyDiagnostics.Write(EntityManager, identities); }
                catch (Exception error) { Mod.Log.Error(error, "Read-only opposing occupancy capture failed; continuing native lane capture."); }
                try { JunctionConnectionDiagnostics.Write(World, EntityManager, selected, identities); }
                catch (Exception error) { Mod.Log.Error(error, "Read-only endpoint junction capture failed; continuing native lane capture."); }
                int capturedCarLaneObjects = 0;
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
                    bool carLane = EntityManager.HasComponent<Game.Net.CarLane>(lane);
                    if (carLane)
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
                        if (carLane) capturedCarLaneObjects += objects.Length;
                        foreach (var item in objects) Dump("laneObject", item);
                    }
                }
                // Moving cars can be absent from the selected lane's LaneObject buffer
                // at the instant of a capture. Query their authoritative current-lane
                // component, then match that lane to this selected road.
                using (var query = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<Game.Vehicles.CarCurrentLane>()))
                using (var cars = query.ToEntityArray(Allocator.Temp))
                {
                    int activeCars = 0;
                    int selectedCars = 0;
                    int sampledSelectedCars = 0;
                    for (int i = 0; i < cars.Length; i++)
                    {
                        Entity vehicle = cars[i];
                        var current = EntityManager.GetComponentData<Game.Vehicles.CarCurrentLane>(vehicle);
                        if (current.m_Lane == Entity.Null) continue;
                        activeCars++;
                        if (Array.IndexOf(identities, current.m_Lane) < 0) continue;
                        selectedCars++;
                        if (sampledSelectedCars++ < 32) WriteVehicle(vehicle, current, "selectedRoad");
                    }
                    int fallbackCars = 0;
                    if (selectedCars == 0)
                    {
                        for (int i = 0; i < cars.Length && fallbackCars < 8; i++)
                        {
                            Entity vehicle = cars[i];
                            var current = EntityManager.GetComponentData<Game.Vehicles.CarCurrentLane>(vehicle);
                            if (current.m_Lane == Entity.Null) continue;
                            WriteVehicle(vehicle, current, "otherRoad");
                            fallbackCars++;
                        }
                    }
                    Mod.Log.Info($"[CAPTURE] capturedCarLaneObjects={capturedCarLaneObjects} activeCars={activeCars} selectedRoadCars={selectedCars} sampledSelectedCars={Math.Min(selectedCars, 32)} fallbackOtherRoadCars={fallbackCars}; route inspection is read-only");
                }
            }
            catch (Exception error) { Mod.Log.Error(error, "Read-only lane capture failed"); }
            finally { Mod.Log.Info($"[CAPTURE] elapsedMs={timer.Elapsed.TotalMilliseconds:F3}; this is diagnostic capture time, not traffic-system overhead."); }
        }

        private void WriteVehicle(Entity vehicle, Game.Vehicles.CarCurrentLane current, string scope)
        {
            Mod.Log.Info($"[VEHICLE] activeCar={vehicle} scope={scope} currentLane={current.m_Lane}");
            if (EntityManager.HasComponent<Game.Vehicles.CarNavigation>(vehicle))
                Dump("carNavigation", EntityManager.GetComponentData<Game.Vehicles.CarNavigation>(vehicle));
            Dump("carCurrentLane", current);
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
                for (int i = 0; i < Math.Min(navigation.Length, 16); i++)
                {
                    var segment = navigation[i];
                    Dump("navigationLane", segment);
                    if (i < 8) Mod.Log.Info("[NAV] " + RouteTargetDiagnostics.Describe(EntityManager, segment));
                }
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
                for (int i = 0; i < Math.Min(remaining, 16); i++)
                {
                    var element = path[first + i];
                    Dump("pathElement", element);
                    if (i < 8) Mod.Log.Info("[ROUTE] " + RouteTargetDiagnostics.Describe(EntityManager, element));
                }
            }
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
