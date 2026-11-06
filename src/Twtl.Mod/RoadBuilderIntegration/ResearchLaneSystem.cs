using System;
using Game;
using Game.Prefabs;
using RoadBuilder.Domain.Components.Prefabs;
using RoadBuilder.Domain.Enums;
using RoadBuilder.Systems;
using RoadBuilder.Utilities;

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
                // Verify recursively using Road Builder's own public helper. Refuse a changed upstream template.
                if (source.FindLanes<Game.Prefabs.CarLane>().Count != 0 || source.FindLanes<TrackLane>().Count != 0)
                    throw new InvalidOperationException("Empty template contains drivable lanes; research registration refused.");
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
