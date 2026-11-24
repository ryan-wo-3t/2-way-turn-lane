using System;
using Game.Prefabs;
using RoadBuilder.Domain.Prefabs;
using RoadBuilder.Systems;
using RoadBuilder.Utilities;
using Unity.Entities;

namespace Twtl.Mod.Diagnostics
{
    /// <summary>One-shot inspection of the road prefab's Road Builder configuration.</summary>
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
            if (!prefabSystem.TryGetSpecificPrefab<NetGeometryPrefab>(reference, out var road))
            {
                Mod.Log.Warn("[CONFIG] Selected owner has no road geometry prefab; configuration unknown.");
                return;
            }

            // Road Builder can edit a stock road before it has a saved custom prefab.
            // Its public method derives a configuration from that prefab in this case.
            // This is a one-shot diagnostic, never traffic activation evidence.
            bool saved = road is INetworkBuilderPrefab;
            var config = saved
                ? ((INetworkBuilderPrefab)road).Config
                : world.GetExistingSystemManaged<RoadBuilderSystem>()?.GetOrGenerateConfiguration(owner);
            if (config == null)
            {
                Mod.Log.Warn("[CONFIG] Road Builder could not resolve a configuration for this road prefab.");
                return;
            }
            var catalogue = world.GetExistingSystemManaged<RoadBuilderGenerationDataSystem>();
            int count = Math.Min(config.Lanes.Count, MaximumSections);
            int closedCount = 0;
            bool closedHasLanes = false;
            bool closedFullyInspected = true;
            Mod.Log.Info($"[CONFIG] road={road.name} source={(saved ? "saved-prefab" : "generated-from-stock-prefab")} sections={config.Lanes.Count} captured={count} category={config.Category}; prefab configuration only, unsaved editor changes excluded");
            RoadBuilderCrossSectionDiagnostics.Write(world, config);
            for (int i = 0; i < count; i++)
            {
                var lane = config.Lanes[i];
                string sectionName = lane.SectionPrefabName ?? "";
                Mod.Log.Info($"[CONFIG] index={i} section={sectionName} group={lane.GroupPrefabName} invert={lane.Invert}");
                if (sectionName == RoadBuilderIntegration.ResearchLaneSystem.PrefabName) closedCount++;
                if (catalogue?.RoadGenerationData == null ||
                    !NetworkPrefabGenerationUtil.GetNetSection(catalogue.RoadGenerationData, config, lane, out var section, out _) ||
                    section == null)
                {
                    if (sectionName == RoadBuilderIntegration.ResearchLaneSystem.PrefabName) closedFullyInspected = false;
                    Mod.Log.Info($"[CONFIG] index={i} section variant unresolved; piece lanes unknown");
                    continue;
                }
                Mod.Log.Info($"[CONFIG] index={i} resolvedSection={section.name}; Road Builder selected variant");
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
