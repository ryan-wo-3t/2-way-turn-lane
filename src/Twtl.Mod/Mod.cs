using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.Prefabs;
using Game.SceneFlow;
using Twtl.Mod.Diagnostics;
using Twtl.Mod.RoadBuilderIntegration;
using Unity.Entities;

namespace Twtl.Mod
{
    public sealed class Mod : IMod
    {
        internal static readonly ILog Log = LogManager.GetLogger("TwoWayTurnLane").SetShowsErrorsInUI(true);
        internal static Settings? Options;
        private ResearchLaneSystem? research;
        private NativeFixtureSystem? nativeFixture;
        private MedianGraphFixtureSystem? medianGraphFixture;
        private SeparatorGraphFixtureSystem? separatorGraphFixture;
        private VirtualLaneGraphFixtureSystem? virtualLaneGraphFixture;
        private ExperimentalLaneClosureSystem? experimentalLaneClosure;
        private SelectedRoadDiagnosticsSystem? diagnostics;
        private PrototypeTrafficMonitorSystem? trafficMonitor;

        public void OnLoad(UpdateSystem updateSystem)
        {
            Options = new Settings(this);
            AssetDatabase.global.LoadSettings("TwoWayTurnLane", Options, new Settings(this));
            Options.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new EnglishLocale(Options));
            ApiAudit.Write(Log);
            Log.Warn("Research build: functional shared-lane driving is unavailable. Experimental center lanes are closed to native pathfinding except the explicit disposable-city drivable prototype; turn-only routing and conflict controls remain unverified.");
            // Options can be enabled after prefab initialization has gone idle. Road Builder
            // also registers its saved road prefabs from MainLoop after generation data exists.
            // AddPrefab creates Created entities immediately. Run before PrefabSystem so its
            // same-frame PrefabUpdate lets Road Builder discover them before marker cleanup.
            updateSystem.UpdateBefore<ResearchLaneSystem, PrefabSystem>(SystemUpdatePhase.MainLoop);
            updateSystem.UpdateBefore<NativeFixtureSystem, PrefabSystem>(SystemUpdatePhase.MainLoop);
            updateSystem.UpdateBefore<MedianGraphFixtureSystem, PrefabSystem>(SystemUpdatePhase.MainLoop);
            updateSystem.UpdateBefore<SeparatorGraphFixtureSystem, PrefabSystem>(SystemUpdatePhase.MainLoop);
            updateSystem.UpdateBefore<VirtualLaneGraphFixtureSystem, PrefabSystem>(SystemUpdatePhase.MainLoop);
            updateSystem.UpdateAfter<ExperimentalLaneClosureSystem, Game.Pathfind.LaneDataSystem>(SystemUpdatePhase.ModificationEnd);
            updateSystem.UpdateBefore<ExperimentalLaneClosureSystem, Game.Pathfind.LanesModifiedSystem>(SystemUpdatePhase.ModificationEnd);
            updateSystem.UpdateAt<SelectedRoadDiagnosticsSystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<PrototypeTrafficMonitorSystem>(SystemUpdatePhase.UIUpdate);
            research = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<ResearchLaneSystem>();
            nativeFixture = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<NativeFixtureSystem>();
            medianGraphFixture = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<MedianGraphFixtureSystem>();
            separatorGraphFixture = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<SeparatorGraphFixtureSystem>();
            virtualLaneGraphFixture = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<VirtualLaneGraphFixtureSystem>();
            experimentalLaneClosure = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<ExperimentalLaneClosureSystem>();
            diagnostics = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<SelectedRoadDiagnosticsSystem>();
            trafficMonitor = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<PrototypeTrafficMonitorSystem>();
        }

        public void OnDispose()
        {
            if (research != null) research.Enabled = false;
            if (nativeFixture != null) nativeFixture.Enabled = false;
            if (medianGraphFixture != null) medianGraphFixture.Enabled = false;
            if (separatorGraphFixture != null) separatorGraphFixture.Enabled = false;
            if (virtualLaneGraphFixture != null) virtualLaneGraphFixture.Enabled = false;
            if (experimentalLaneClosure != null) experimentalLaneClosure.Enabled = false;
            if (diagnostics != null) diagnostics.Enabled = false;
            if (trafficMonitor != null) trafficMonitor.Enabled = false;
            Options?.UnregisterInOptionsUI();
            Options = null;
            // The pathfinder blockage gate writes lane data. Live prefab removal
            // or blanket unblock on disposal could expose an unsafe saved road.
            // No navigation patches or transient reservations exist yet.
            // Registered prefabs belong to the world; removing a live prefab could damage roads.
        }
    }
}
