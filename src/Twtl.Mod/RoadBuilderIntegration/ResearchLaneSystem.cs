using System;
using System.Linq;
using Game;
using Game.Prefabs;
using RoadBuilder.Domain.Components.Prefabs;
using RoadBuilder.Domain.Enums;
using RoadBuilder.Systems;
using UnityEngine;

namespace Twtl.Mod.RoadBuilderIntegration
{
    /// <summary>External lane discovery experiment. Never creates vehicle lanes or changes existing sections.</summary>
    public partial class ResearchLaneSystem : GameSystemBase
    {
        public const string PrefabName = "TWTL Research Corridor CLOSED 3m";
        private bool registered;
        private bool failed;
        private bool discovered;
        protected override void OnUpdate()
        {
            if (failed) return;
            var roadBuilder = World.GetExistingSystemManaged<RoadBuilderGenerationDataSystem>();
            if (registered)
            {
                if (!discovered && roadBuilder != null && roadBuilder.NetSections.ContainsKey(PrefabName))
                {
                    discovered = true;
                    Mod.Log.Info("[RESEARCH] Road Builder discovered the CLOSED section in its public section catalogue; picker and placed-road checks still required.");
                }
                return;
            }
            if (Mod.Options?.ShowClosedResearchLane != true) return;
            if (roadBuilder?.RoadGenerationData == null ||
                !roadBuilder.NetSections.TryGetValue("RB Empty Section 3", out var source)) return;
            try
            {
                // FindLanes<T> ignores conditional pieces and does not traverse subsections.
                // Check every piece, including conditional variants, and reject subsections.
                if ((source.m_SubSections?.Length ?? 0) != 0 || source.m_Pieces == null || source.m_Pieces.Length == 0 ||
                    source.m_Pieces.Any(piece => piece.m_Piece == null || piece.m_Piece.TryGet<NetPieceLanes>(out _)))
                    throw new InvalidOperationException("Empty template has lane-bearing pieces, subsections or incomplete geometry; registration refused.");
                // PrefabBase.Clone serializes all attached components through Unity JSON.
                // Road Builder's ulong category enum is unsupported there in the installed
                // game. Construct only the verified empty geometry and our own metadata;
                // never clone/serialize upstream group links or ownership flags.
                var section = ScriptableObject.CreateInstance<NetSectionPrefab>();
                section.name = PrefabName;
                section.m_SubSections = Array.Empty<NetSubSectionInfo>();
                section.m_Pieces = (NetPieceInfo[])source.m_Pieces.Clone();
                var info = section.AddOrGetComponent<RoadBuilderLaneInfo>();
                info.RequireAll = RoadCategory.Road;
                info.RequireAny = RoadCategory.Road;
                info.RequireNone = RoadCategory.NonRoad;
                info.NoDirection = true;
                info.Median = false;
                if (!World.GetOrCreateSystemManaged<PrefabSystem>().AddPrefab(section))
                    throw new InvalidOperationException("PrefabSystem rejected the research section; registration did not succeed.");
                registered = true;
                Mod.Log.Info("[RESEARCH] Registered CLOSED non-drivable research corridor; waiting for Road Builder catalogue discovery.");
            }
            catch (Exception error)
            {
                failed = true;
                Mod.Log.Error(error, "Research lane registration failed; traffic remains untouched.");
            }
        }
    }
}
