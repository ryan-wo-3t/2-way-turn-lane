using System;
using System.Collections.Generic;
using Game;
using Game.Net;
using Game.Pathfind;
using Game.Vehicles;
using RoadBuilder.Systems;
using RoadBuilder.Systems.UI;
using Twtl.Core;
using Twtl.Mod.RoadBuilderIntegration;
using Unity.Collections;
using Unity.Entities;

namespace Twtl.Mod.Diagnostics
{
    /// <summary>
    /// Bounded, opt-in observation of actual cars on a selected V3 road. The
    /// one-shot graph probe can miss traffic between frames; this samples for
    /// two minutes without changing paths, lanes, reservations or vehicles.
    /// </summary>
    public partial class PrototypeTrafficMonitorSystem : GameSystemBase
    {
        private static readonly TimeSpan Duration = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);
        private readonly HashSet<Entity> ordinaryLanes = new HashSet<Entity>();
        private readonly HashSet<Entity> centerLanes = new HashSet<Entity>();
        private readonly HashSet<Entity> seenOrdinaryCars = new HashSet<Entity>();
        private readonly HashSet<Entity> seenCenterCars = new HashSet<Entity>();
        private readonly HashSet<Entity> plannedCenterCars = new HashSet<Entity>();
        private readonly HashSet<Entity> currentlyOnCenter = new HashSet<Entity>();
        private readonly HashSet<Entity> nextOnCenter = new HashSet<Entity>();
        private readonly Dictionary<Entity, CenterVisit> centerVisits = new Dictionary<Entity, CenterVisit>();
        private Entity owner;
        private DateTime started;
        private DateTime nextSample;
        private int samples;
        private int ordinaryCarFrames;
        private int centerCarFrames;
        private int centerEntries;
        private int opposingSamples;
        private readonly Dictionary<TraceResult, int> routeShapes = new Dictionary<TraceResult, int>();
        private readonly Dictionary<TraceResult, int> plannedRouteShapes = new Dictionary<TraceResult, int>();
        private bool running;

        private sealed class CenterVisit
        {
            public float First;
            public float Last;
            public float Min;
            public float Max;
            public int Samples;
            public int Direction;

            public CenterVisit(float position)
            {
                First = Last = Min = Max = position;
            }

            public void Observe(float position)
            {
                float change = position - Last;
                if (change > 0.001f) Direction = 1;
                else if (change < -0.001f) Direction = -1;
                Last = position;
                Min = Math.Min(Min, position);
                Max = Math.Max(Max, position);
                Samples++;
            }
        }

        protected override void OnUpdate()
        {
            if (Mod.Options?.MonitorPrototypeTraffic == true)
            {
                Mod.Options.MonitorPrototypeTraffic = false;
                Start();
            }
            if (!running || DateTime.UtcNow < nextSample) return;
            if (DateTime.UtcNow - started >= Duration)
            {
                Finish("two-minute window complete");
                return;
            }
            if (!EntityManager.Exists(owner) || !EntityManager.HasBuffer<SubLane>(owner))
            {
                Finish("selected road was removed");
                return;
            }
            nextSample = DateTime.UtcNow + Interval;
            Sample();
        }

        private void Start()
        {
            if (running) Finish("restarted by user");
            var builder = World.GetExistingSystemManaged<RoadBuilderSystem>();
            owner = World.GetExistingSystemManaged<RoadBuilderUISystem>()?.WorkingEntity ?? Entity.Null;
            if (builder == null || owner == Entity.Null || !EntityManager.Exists(owner) ||
                !EntityManager.HasBuffer<SubLane>(owner) ||
                builder.GetOrGenerateConfiguration(owner)?.ID != VirtualLaneGraphFixtureSystem.DrivableRoadId)
            {
                Mod.Log.Warn("[TRAFFIC-MONITOR] Select the V3 sidewalk prototype in Road Builder before starting the two-minute car sample.");
                return;
            }

            ordinaryLanes.Clear();
            centerLanes.Clear();
            seenOrdinaryCars.Clear();
            seenCenterCars.Clear();
            plannedCenterCars.Clear();
            currentlyOnCenter.Clear();
            nextOnCenter.Clear();
            centerVisits.Clear();
            routeShapes.Clear();
            plannedRouteShapes.Clear();
            samples = ordinaryCarFrames = centerCarFrames = centerEntries = opposingSamples = 0;
            var subLanes = EntityManager.GetBuffer<SubLane>(owner, true);
            for (int i = 0; i < subLanes.Length; i++)
            {
                Entity lane = subLanes[i].m_SubLane;
                if (!EntityManager.Exists(lane) || !EntityManager.HasComponent<Game.Net.CarLane>(lane)) continue;
                var carLane = EntityManager.GetComponentData<Game.Net.CarLane>(lane);
                if ((carLane.m_Flags & Game.Net.CarLaneFlags.Twoway) != 0) centerLanes.Add(lane);
                else ordinaryLanes.Add(lane);
            }
            if (centerLanes.Count == 0 || ordinaryLanes.Count == 0)
            {
                Mod.Log.Warn($"[TRAFFIC-MONITOR] Refused owner={owner}: centerLanes={centerLanes.Count} ordinaryLanes={ordinaryLanes.Count}.");
                return;
            }
            started = nextSample = DateTime.UtcNow;
            running = true;
            Mod.Log.Info($"[TRAFFIC-MONITOR] Started owner={owner} ordinaryLanes={ordinaryLanes.Count} centerLanes={centerLanes.Count} windowSeconds={Duration.TotalSeconds}; read-only actual-car observation.");
        }

        private void Sample()
        {
            nextOnCenter.Clear();
            int ordinary = 0;
            int center = 0;
            int forwardCenter = 0;
            int backwardCenter = 0;
            using (var query = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<CarCurrentLane>()))
            using (var cars = query.ToEntityArray(Allocator.Temp))
            {
                for (int i = 0; i < cars.Length; i++)
                {
                    Entity vehicle = cars[i];
                    var current = EntityManager.GetComponentData<CarCurrentLane>(vehicle);
                    if (ordinaryLanes.Contains(current.m_Lane))
                    {
                        ordinary++;
                        seenOrdinaryCars.Add(vehicle);
                        if (!plannedCenterCars.Contains(vehicle) && TryAssessRouteShape(vehicle, out var plannedShape))
                        {
                            plannedCenterCars.Add(vehicle);
                            plannedRouteShapes.TryGetValue(plannedShape, out int count);
                            plannedRouteShapes[plannedShape] = count + 1;
                            if (plannedCenterCars.Count <= 12)
                                Mod.Log.Info($"[TRAFFIC-MONITOR] plannedCenter vehicle={vehicle} currentLane={current.m_Lane} routeShape={plannedShape}; planned route is observational, not turn authorization.");
                        }
                    }
                    else if (centerLanes.Contains(current.m_Lane))
                    {
                        center++;
                        seenCenterCars.Add(vehicle);
                        nextOnCenter.Add(vehicle);
                        float position = current.m_CurvePosition.x;
                        if (!centerVisits.TryGetValue(vehicle, out var visit))
                        {
                            visit = new CenterVisit(position);
                            centerVisits.Add(vehicle, visit);
                        }
                        visit.Observe(position);
                        if (visit.Direction > 0) forwardCenter++;
                        else if (visit.Direction < 0) backwardCenter++;
                        if (currentlyOnCenter.Contains(vehicle)) continue;
                        centerEntries++;
                        TraceResult shape = TryAssessRouteShape(vehicle, out var observedShape)
                            ? observedShape : TraceResult.Incomplete;
                        routeShapes.TryGetValue(shape, out int shapeCount);
                        routeShapes[shape] = shapeCount + 1;
                        Mod.Log.Info($"[TRAFFIC-MONITOR] centerEntry vehicle={vehicle} lane={current.m_Lane} curvePosition={current.m_CurvePosition.x:F3} routeShape={shape} navigation={DescribeNavigation(vehicle)}; possible roadside turns are not authorized movements.");
                    }
                }
            }
            foreach (Entity vehicle in currentlyOnCenter)
            {
                if (nextOnCenter.Contains(vehicle) || !centerVisits.TryGetValue(vehicle, out var visit)) continue;
                string exitLane = EntityManager.Exists(vehicle) && EntityManager.HasComponent<CarCurrentLane>(vehicle)
                    ? EntityManager.GetComponentData<CarCurrentLane>(vehicle).m_Lane.ToString() : "despawned";
                Mod.Log.Info($"[TRAFFIC-MONITOR] centerExit vehicle={vehicle} entry={visit.First:F3} exit={visit.Last:F3} range={visit.Min:F3}..{visit.Max:F3} direction={visit.Direction} observedSeconds={visit.Samples} nextLane={exitLane}; route purpose unverified.");
                centerVisits.Remove(vehicle);
            }
            if (forwardCenter > 0 && backwardCenter > 0)
            {
                opposingSamples++;
                Mod.Log.Warn($"[TRAFFIC-MONITOR] opposingOccupancy sample={samples + 1} forward={forwardCenter} backward={backwardCenter}; simultaneous opposite movement observed, braking and separation unverified.");
            }
            samples++;
            ordinaryCarFrames += ordinary;
            centerCarFrames += center;
            currentlyOnCenter.Clear();
            foreach (Entity vehicle in nextOnCenter) currentlyOnCenter.Add(vehicle);
            if (samples == 1 || samples % 10 == 0 || center > 0)
                Mod.Log.Info($"[TRAFFIC-MONITOR] sample={samples} ordinaryCars={ordinary} centerCars={center} uniqueOrdinary={seenOrdinaryCars.Count} uniqueCenter={seenCenterCars.Count}; no vehicle control.");
        }

        private string DescribeNavigation(Entity vehicle)
        {
            if (!EntityManager.HasBuffer<CarNavigationLane>(vehicle)) return "none";
            var navigation = EntityManager.GetBuffer<CarNavigationLane>(vehicle, true);
            int count = Math.Min(navigation.Length, 6);
            var parts = new string[count];
            for (int i = 0; i < count; i++)
                parts[i] = RouteTargetDiagnostics.Describe(EntityManager, navigation[i]);
            return string.Join(" | ", parts);
        }

        private bool TryAssessRouteShape(Entity vehicle, out TraceResult result)
        {
            result = TraceResult.Incomplete;
            if (EntityManager.HasBuffer<CarNavigationLane>(vehicle))
            {
                var navigation = EntityManager.GetBuffer<CarNavigationLane>(vehicle, true);
                int count = Math.Min(navigation.Length, 16);
                var steps = new TraceStep[count];
                int navigationCenterIndex = -1;
                for (int i = 0; i < count; i++)
                {
                    var item = navigation[i];
                    if (centerLanes.Contains(item.m_Lane) && navigationCenterIndex < 0) navigationCenterIndex = i;
                    steps[i] = MakeTraceStep(item.m_Lane, item.m_CurvePosition.x,
                        item.m_CurvePosition.y,
                        (item.m_Flags & Game.Vehicles.CarLaneFlags.TurnLeft) != 0,
                        (item.m_Flags & Game.Vehicles.CarLaneFlags.TurnRight) != 0);
                }
                if (navigationCenterIndex >= 0)
                {
                    result = RouteTracePolicy.Assess(steps, navigationCenterIndex, Handedness.Right, new TwtlOptions());
                    return true;
                }
            }
            if (!EntityManager.HasBuffer<PathElement>(vehicle) ||
                !EntityManager.HasComponent<PathOwner>(vehicle)) return false;
            var path = EntityManager.GetBuffer<PathElement>(vehicle, true);
            int start = Math.Max(0, EntityManager.GetComponentData<PathOwner>(vehicle).m_ElementIndex);
            if (start >= path.Length) return false;
            int end = start + Math.Min(32, path.Length - start);
            var pending = new TraceStep[end - start];
            int centerIndex = -1;
            for (int i = start; i < end; i++)
            {
                var item = path[i];
                if (centerLanes.Contains(item.m_Target) && centerIndex < 0) centerIndex = i - start;
                bool left = false, right = false;
                if (EntityManager.Exists(item.m_Target) &&
                    EntityManager.HasComponent<Game.Net.CarLane>(item.m_Target))
                {
                    var flags = EntityManager.GetComponentData<Game.Net.CarLane>(item.m_Target).m_Flags;
                    left = (flags & (Game.Net.CarLaneFlags.TurnLeft | Game.Net.CarLaneFlags.GentleTurnLeft)) != 0;
                    right = (flags & (Game.Net.CarLaneFlags.TurnRight | Game.Net.CarLaneFlags.GentleTurnRight)) != 0;
                }
                else if (EntityManager.Exists(item.m_Target) &&
                    EntityManager.HasComponent<ParkingLane>(item.m_Target))
                {
                    var flags = EntityManager.GetComponentData<ParkingLane>(item.m_Target).m_Flags;
                    left = (flags & ParkingLaneFlags.ParkingLeft) != 0;
                    right = (flags & ParkingLaneFlags.ParkingRight) != 0;
                }
                pending[i - start] = MakeTraceStep(item.m_Target, item.m_TargetDelta.x,
                    item.m_TargetDelta.y, left, right);
            }
            if (centerIndex < 0) return false;
            result = RouteTracePolicy.Assess(pending, centerIndex, Handedness.Right, new TwtlOptions());
            return true;
        }

        private TraceStep MakeTraceStep(Entity lane, float from, float to, bool left, bool right)
        {
            TraceLaneKind kind = TraceLaneKind.Unknown;
            double metres = 0;
            if (EntityManager.Exists(lane))
            {
                if (centerLanes.Contains(lane)) kind = TraceLaneKind.SharedCenter;
                else if (EntityManager.HasComponent<NodeLane>(lane)) kind = TraceLaneKind.Junction;
                else if (EntityManager.HasComponent<ConnectionLane>(lane)) kind = TraceLaneKind.RoadsideConnection;
                else if (EntityManager.HasComponent<ParkingLane>(lane)) kind = TraceLaneKind.Parking;
                else if (EntityManager.HasComponent<EdgeLane>(lane)) kind = TraceLaneKind.Ordinary;
                if (EntityManager.HasComponent<Curve>(lane))
                    metres = EntityManager.GetComponentData<Curve>(lane).m_Length * Math.Abs(to - from);
            }
            TurnSide side = left == right ? TurnSide.Unknown : left ? TurnSide.Left : TurnSide.Right;
            return new TraceStep(kind, metres, side);
        }

        private void Finish(string reason)
        {
            if (!running) return;
            running = false;
            Mod.Log.Info($"[TRAFFIC-MONITOR] Finished reason={reason} owner={owner} samples={samples} ordinaryCarFrames={ordinaryCarFrames} centerCarFrames={centerCarFrames} uniqueOrdinaryCars={seenOrdinaryCars.Count} uniqueCenterCars={seenCenterCars.Count} plannedCenterCars={plannedCenterCars.Count} plannedRouteShapes={string.Join(",", plannedRouteShapes)} centerEntries={centerEntries} opposingSamples={opposingSamples} centerStillObserved={centerVisits.Count} routeShapes={string.Join(",", routeShapes)}; no turn-only or conflict-safety claim.");
        }
    }
}
