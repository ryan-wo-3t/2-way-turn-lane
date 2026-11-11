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
        private SelectedRoadDiagnosticsSystem? diagnostics;

        public void OnLoad(UpdateSystem updateSystem)
        {
            Options = new Settings(this);
            AssetDatabase.global.LoadSettings("TwoWayTurnLane", Options, new Settings(this));
            Options.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new EnglishLocale(Options));
            ApiAudit.Write(Log);
            Log.Warn("Research build: shared-lane driving is unavailable. Only an explicitly opted-in CLOSED non-drivable lane can be registered.");
            // Options can be enabled after prefab initialization has gone idle. Road Builder
            // also registers its saved road prefabs from MainLoop after generation data exists.
            // AddPrefab creates Created entities immediately. Run before PrefabSystem so its
            // same-frame PrefabUpdate lets Road Builder discover them before marker cleanup.
            updateSystem.UpdateBefore<ResearchLaneSystem, PrefabSystem>(SystemUpdatePhase.MainLoop);
            updateSystem.UpdateAt<SelectedRoadDiagnosticsSystem>(SystemUpdatePhase.UIUpdate);
            research = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<ResearchLaneSystem>();
            diagnostics = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<SelectedRoadDiagnosticsSystem>();
        }

        public void OnDispose()
        {
            if (research != null) research.Enabled = false;
            if (diagnostics != null) diagnostics.Enabled = false;
            Options?.UnregisterInOptionsUI();
            Options = null;
            // No traffic writes, navigation patches or transient reservations exist in this build.
            // Registered prefabs belong to the world; removing a live prefab could damage roads.
        }
    }
}
