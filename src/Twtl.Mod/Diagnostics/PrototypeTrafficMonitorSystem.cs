using System;
using System.Collections.Generic;
using Game;
using Game.Net;
using Game.Vehicles;
using RoadBuilder.Systems;
using RoadBuilder.Systems.UI;
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
            currentlyOnCenter.Clear();
            nextOnCenter.Clear();
            centerVisits.Clear();
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
                        Mod.Log.Info($"[TRAFFIC-MONITOR] centerEntry vehicle={vehicle} lane={current.m_Lane} curvePosition={current.m_CurvePosition.x:F3} navigation={DescribeNavigation(vehicle)}; intent is observational, not turn authorization.");
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

        private void Finish(string reason)
        {
            if (!running) return;
            running = false;
            Mod.Log.Info($"[TRAFFIC-MONITOR] Finished reason={reason} owner={owner} samples={samples} ordinaryCarFrames={ordinaryCarFrames} centerCarFrames={centerCarFrames} uniqueOrdinaryCars={seenOrdinaryCars.Count} uniqueCenterCars={seenCenterCars.Count} centerEntries={centerEntries} opposingSamples={opposingSamples} centerStillObserved={centerVisits.Count}; no turn-only or conflict-safety claim.");
        }
    }
}
