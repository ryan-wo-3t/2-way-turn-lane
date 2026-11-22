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
        private const int MaximumSectionReferences = 24;
        private const string CandidateName = "Invisible Car Twoway Lane 3";

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

            if (!catalogue.NetLanes.TryGetValue(CandidateName, out var candidate))
            {
                Mod.Log.Warn($"[PREFABS] {CandidateName} is unavailable; section references not inspected.");
                return;
            }

            int referenceCount = 0;
            foreach (var section in catalogue.NetSections.Values.OrderBy(section => section.name, StringComparer.Ordinal))
            {
                if (section?.m_Pieces == null) continue;
                foreach (var pieceInfo in section.m_Pieces)
                {
                    var piece = pieceInfo.m_Piece;
                    if (piece == null || !piece.TryGet<NetPieceLanes>(out var pieceLanes) || pieceLanes.m_Lanes == null)
                        continue;
                    if (!pieceLanes.m_Lanes.Any(info => info.m_Lane == candidate)) continue;
                    referenceCount++;
                    if (referenceCount <= MaximumSectionReferences)
                        Mod.Log.Info($"[PREFABS] twoWaySection={section.name} piece={piece.name}; direct source prefab reference, conditional piece activity unknown");
                }
            }
            Mod.Log.Info($"[PREFABS] directTwoWaySectionReferences={referenceCount} captured={Math.Min(referenceCount, MaximumSectionReferences)}; nested subsections not traversed, not generated-graph evidence");
        }
    }
}
