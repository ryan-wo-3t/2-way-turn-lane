using System;
using Game.Prefabs;
using RoadBuilder.Domain.Prefabs;
using RoadBuilder.Systems;
using RoadBuilder.Utilities;
using Unity.Entities;

namespace Twtl.Mod.Diagnostics
{
    /// <summary>One-shot inspection of the saved Road Builder configuration behind a selected owner.</summary>
    internal static class RoadBuilderConfigurationDiagnostics
    {
        private const int MaximumSections = 64;
        private const int MaximumPieceLanes = 16;

        public static void Write(World world, EntityManager manager, Entity owner)
        {
            if (!manager.HasComponent<PrefabRef>(owner))
            {
                Mod.Log.Warn("[CONFIG] Selected owner has no PrefabRef; Road Builder configuration unknown.");
                return;
            }
            var prefabSystem = world.GetOrCreateSystemManaged<PrefabSystem>();
            var reference = manager.GetComponentData<PrefabRef>(owner);
            if (!prefabSystem.TryGetSpecificPrefab<NetGeometryPrefab>(reference, out var road) ||
                road is not INetworkBuilderPrefab builder)
            {
                Mod.Log.Warn("[CONFIG] Selected owner is not a saved Road Builder road; configuration unknown.");
                return;
            }

            var config = builder.Config;
            if (config == null)
            {
                Mod.Log.Warn("[CONFIG] Road Builder prefab has no saved configuration.");
                return;
            }
            var catalogue = world.GetExistingSystemManaged<RoadBuilderGenerationDataSystem>();
            int count = Math.Min(config.Lanes.Count, MaximumSections);
            int closedCount = 0;
            bool closedHasLanes = false;
            bool closedFullyInspected = true;
            Mod.Log.Info($"[CONFIG] road={road.name} sections={config.Lanes.Count} captured={count} category={config.Category}; read-only source configuration");
            for (int i = 0; i < count; i++)
            {
                var lane = config.Lanes[i];
                string sectionName = lane.SectionPrefabName ?? "";
                Mod.Log.Info($"[CONFIG] index={i} section={sectionName} group={lane.GroupPrefabName} invert={lane.Invert}");
                if (sectionName == RoadBuilderIntegration.ResearchLaneSystem.PrefabName) closedCount++;
                if (sectionName.Length == 0 || catalogue == null || !catalogue.NetSections.TryGetValue(sectionName, out var section))
                {
                    if (sectionName == RoadBuilderIntegration.ResearchLaneSystem.PrefabName) closedFullyInspected = false;
                    Mod.Log.Info($"[CONFIG] index={i} section prefab unavailable or group-based; piece lanes unknown");
                    continue;
                }
                var subSections = section.m_SubSections;
                if (subSections != null && subSections.Length > 0)
                {
                    if (sectionName == RoadBuilderIntegration.ResearchLaneSystem.PrefabName) closedFullyInspected = false;
                    Mod.Log.Info($"[CONFIG] index={i} subsections={subSections.Length}; nested lanes not inspected");
                }
                var pieces = section.m_Pieces;
                if (pieces == null || pieces.Length == 0)
                {
                    if (sectionName == RoadBuilderIntegration.ResearchLaneSystem.PrefabName) closedFullyInspected = false;
                    continue;
                }
                for (int pieceIndex = 0; pieceIndex < pieces.Length; pieceIndex++)
                {
                    var piece = pieces[pieceIndex].m_Piece;
                    if (piece == null)
                    {
                        if (sectionName == RoadBuilderIntegration.ResearchLaneSystem.PrefabName) closedFullyInspected = false;
                        continue;
                    }
                    if (!piece.TryGet<NetPieceLanes>(out var pieceLanes)) continue;
                    var lanePrefabs = pieceLanes.m_Lanes;
                    int total = lanePrefabs?.Length ?? 0;
                    if (sectionName == RoadBuilderIntegration.ResearchLaneSystem.PrefabName && total > 0)
                        closedHasLanes = true;
                    Mod.Log.Info($"[CONFIG] index={i} piece={piece.name} pieceIndex={pieceIndex} lanePrefabs={total} captured={Math.Min(total, MaximumPieceLanes)}; conditional pieces may be inactive");
                    for (int j = 0; j < Math.Min(total, MaximumPieceLanes); j++)
                    {
                        var info = lanePrefabs![j];
                        bool car = info.m_Lane != null && info.m_Lane.TryGet<Game.Prefabs.CarLane>(out _);
                        Mod.Log.Info($"[CONFIG] index={i} pieceIndex={pieceIndex} laneIndex={j} prefab={info.m_Lane?.name} carPrefab={car} position={info.m_Position}");
                    }
                }
            }
            Mod.Log.Info($"[CONFIG] closedSectionCount={closedCount} closedSectionFullyInspected={closedFullyInspected && config.Lanes.Count <= MaximumSections} closedSectionHasPieceLanes={closedHasLanes}; configuration evidence only, not generated-graph or traffic certification");
        }
    }
}
