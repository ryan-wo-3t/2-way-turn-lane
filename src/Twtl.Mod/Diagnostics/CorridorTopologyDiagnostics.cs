using System;
using System.Collections.Generic;
using Game.Net;
using Twtl.Core;
using Unity.Entities;
using Unity.Mathematics;

namespace Twtl.Mod.Diagnostics
{
    internal static class CorridorTopologyDiagnostics
    {
        // One-shot selected-owner capture only; never a city-wide vehicle scan.
        private const double GeometryToleranceMetres = 0.05;
        private const int MaximumOverlapCapture = 256;

        public static void Write(EntityManager manager, Entity[] lanes)
        {
            var snapshots = new List<LaneSnapshot>();
            foreach (var lane in lanes)
            {
                if (!manager.Exists(lane) || !manager.HasComponent<Curve>(lane) ||
                    !manager.HasComponent<Game.Net.CarLane>(lane)) continue;
                var curve = manager.GetComponentData<Curve>(lane);
                var b = curve.m_Bezier;
                if (!math.all(math.isfinite(b.a)) || !math.all(math.isfinite(b.b)) ||
                    !math.all(math.isfinite(b.c)) || !math.all(math.isfinite(b.d)))
                { Mod.Log.Warn("[TOPOLOGY] non-finite geometry lane=" + lane); continue; }
                var overlaps = Array.Empty<LaneOverlap>();
                int total = 0;
                if (manager.HasBuffer<LaneOverlap>(lane))
                {
                    var buffer = manager.GetBuffer<LaneOverlap>(lane, true);
                    total = buffer.Length;
                    overlaps = new LaneOverlap[Math.Min(total, MaximumOverlapCapture)];
                    for (int i = 0; i < overlaps.Length; i++) overlaps[i] = buffer[i];
                }
                snapshots.Add(new LaneSnapshot(lane, new CorridorCurve(Point(b.a), Point(b.b), Point(b.c), Point(b.d)), overlaps, total));
            }
            int pairs = 0;
            for (int i = 0; i < snapshots.Count; i++)
                for (int j = i + 1; j < snapshots.Count; j++)
                {
                    var a = snapshots[i]; var b = snapshots[j];
                    if (!a.Curve.IsReverseOf(b.Curve, GeometryToleranceMetres)) continue;
                    pairs++;
                    Mod.Log.Info($"[TOPOLOGY] reverseCurveCandidate a={a.Entity} b={b.Entity} toleranceMetres={GeometryToleranceMetres}; geometry only, traffic safety UNKNOWN");
                    WriteOverlaps(a, b.Entity);
                    WriteOverlaps(b, a.Entity);
                }
            Mod.Log.Info($"[TOPOLOGY] inspectedCarLanes={snapshots.Count} reverseCurvePairs={pairs}; bounded selected-owner observation, no graph or braking certification");
        }

        private static Position3 Point(float3 p) => new Position3(p.x, p.y, p.z);

        private static void WriteOverlaps(LaneSnapshot source, Entity other)
        {
            int matches = 0;
            foreach (var overlap in source.Overlaps)
            {
                if (overlap.m_Other != other) continue;
                matches++;
                Mod.Log.Info($"[TOPOLOGY] from={source.Entity} to={other} rawThis=[{overlap.m_ThisStart},{overlap.m_ThisEnd}] rawOther=[{overlap.m_OtherStart},{overlap.m_OtherEnd}] flags={overlap.m_Flags} parallelism={overlap.m_Parallelism} priorityDelta={overlap.m_PriorityDelta}");
            }
            Mod.Log.Info($"[TOPOLOGY] from={source.Entity} to={other} matchingRecords={matches} captured={source.Overlaps.Length} total={source.Total}; absence is conclusive only when capture is complete");
        }

        private sealed class LaneSnapshot
        {
            public Entity Entity { get; }
            public CorridorCurve Curve { get; }
            public LaneOverlap[] Overlaps { get; }
            public int Total { get; }
            public LaneSnapshot(Entity entity, CorridorCurve curve, LaneOverlap[] overlaps, int total)
            { Entity = entity; Curve = curve; Overlaps = overlaps; Total = total; }
        }
    }
}
