using System;
using System.Linq;
using Game;
using Game.Prefabs;
using RoadBuilder.Domain.Components.Prefabs;
using RoadBuilder.Domain.Enums;
using RoadBuilder.Systems;

namespace Twtl.Mod.RoadBuilderIntegration
{
    /// <summary>External lane discovery experiment. Never creates vehicle lanes or changes existing sections.</summary>
    public partial class ResearchLaneSystem : GameSystemBase
    {
        public const string PrefabName = "TWTL Research Corridor CLOSED 3m";
        private bool registered;
        private bool failed;
        protected override void OnUpdate()
        {
            if (registered || failed || Mod.Options?.ShowClosedResearchLane != true) return;
            var roadBuilder = World.GetExistingSystemManaged<RoadBuilderGenerationDataSystem>();
            if (roadBuilder?.RoadGenerationData == null ||
                !roadBuilder.NetSections.TryGetValue("RB Empty Section 3", out var source)) return;
            try
            {
                // FindLanes<T> ignores conditional pieces and does not traverse subsections.
                // Check every piece, including conditional variants, and reject subsections.
                if (source.m_SubSections.Length != 0 || source.m_Pieces.Length == 0 ||
                    source.m_Pieces.Any(piece => piece.m_Piece == null || piece.m_Piece.TryGet<NetPieceLanes>(out _)))
                    throw new InvalidOperationException("Empty template has lane-bearing pieces, subsections or incomplete geometry; registration refused.");
                var section = source.Clone(PrefabName) as NetSectionPrefab
                    ?? throw new InvalidOperationException("Cannot clone empty net section");
                // A cloned RB empty variant must not remain linked to its original group.
                section.Remove<RoadBuilderLaneGroup>();
                var info = section.AddOrGetComponent<RoadBuilderLaneInfo>();
                info.RequireAll = RoadCategory.Road;
                info.RequireAny = RoadCategory.Road;
                info.RequireNone = RoadCategory.NonRoad;
                info.NoDirection = true;
                info.Median = false;
                World.GetOrCreateSystemManaged<PrefabSystem>().AddPrefab(section);
                registered = true;
                Mod.Log.Info("Registered CLOSED non-drivable research corridor. Road Builder discovers the Created prefab and refreshes its own picker.");
            }
            catch (Exception error)
            {
                failed = true;
                Mod.Log.Error(error, "Research lane registration failed; traffic remains untouched.");
            }
        }
    }
}
