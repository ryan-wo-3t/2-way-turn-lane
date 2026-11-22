using System;
using System.Linq;
using Game.Prefabs;
using RoadBuilder.Systems;
using Unity.Entities;

namespace Twtl.Mod.Diagnostics
{
    /// <summary>Read-only catalogue evidence for a future isolated native lane fixture.</summary>
    internal static class NativeLanePrefabDiagnostics
    {
        private const int MaximumCandidates = 24;

        public static void Write(World world)
        {
            var catalogue = world.GetExistingSystemManaged<RoadBuilderGenerationDataSystem>();
            if (catalogue == null)
            {
                Mod.Log.Warn("[PREFABS] Road Builder lane catalogue is unavailable.");
                return;
            }

            var candidates = catalogue.NetLanes.Values
                .Where(lane => lane != null && lane.TryGet<CarLane>(out var car) && car.m_Twoway)
                .OrderBy(lane => lane.name, StringComparer.Ordinal)
                .ToArray();
            Mod.Log.Info($"[PREFABS] lanes={catalogue.NetLanes.Count} nativeTwoWayCarCandidates={candidates.Length} captured={Math.Min(candidates.Length, MaximumCandidates)}; prefab metadata only, not generated lane or traffic evidence");
            foreach (var lane in candidates.Take(MaximumCandidates))
            {
                if (!lane.TryGet<CarLane>(out var car)) continue;
                Mod.Log.Info($"[PREFABS] name={lane.name} width={car.m_Width} roadType={car.m_RoadType} starting={car.m_StartingLane} ending={car.m_EndingLane} twoWay={car.m_Twoway}");
            }
        }
    }
}
