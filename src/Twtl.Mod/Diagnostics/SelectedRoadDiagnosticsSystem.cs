using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Game;
using Game.City;
using Game.Net;
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
                Mod.Log.Info($"[CAPTURE] owner={selected} leftHandTraffic={leftHand}");
                // EntityManager read APIs complete conflicting tracked jobs before accessing buffers.
                // Captures may stall simulation and are never performed continuously.
                var lanes = EntityManager.GetBuffer<SubLane>(selected, true);
                int count = Math.Min(lanes.Length, 64);
                // Copy identities before any subsequent EntityManager calls invalidate buffer handles.
                var identities = new Entity[count];
                for (int i = 0; i < count; i++) identities[i] = lanes[i].m_SubLane;
                Mod.Log.Info($"[CAPTURE] lanes={lanes.Length} captured={count}");
                foreach (Entity lane in identities)
                {
                    if (!EntityManager.Exists(lane)) { Mod.Log.Warn("[CAPTURE] stale lane=" + lane); continue; }
                    Mod.Log.Info("[LANE] " + lane);
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
                        for (int i = 0; i < Math.Min(occupants.Length, 64); i++) Dump("laneObject", occupants[i]);
                    }
                }
            }
            catch (Exception error) { Mod.Log.Error(error, "Read-only lane capture failed"); }
            finally { Mod.Log.Info($"[CAPTURE] elapsedMs={timer.Elapsed.TotalMilliseconds:F3}; this is diagnostic capture time, not traffic-system overhead."); }
        }

        private static void Dump<T>(string label, T data) where T : struct
        {
            string fields = string.Join("; ", typeof(T).GetFields(BindingFlags.Instance | BindingFlags.Public)
                .OrderBy(x => x.Name).Select(field => field.Name + "=" + field.GetValue(data)));
            Mod.Log.Info("[DATA] " + label + " " + fields);
        }
    }
}
