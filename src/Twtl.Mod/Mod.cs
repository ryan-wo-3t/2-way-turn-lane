using Colossal.Logging;
using Game;
using Game.Modding;
using Game.Prefabs;
using Twtl.Mod.RoadBuilderIntegration;
using Unity.Entities;

namespace Twtl.Mod
{
    public sealed class Mod : IMod
    {
        internal static readonly ILog Log = LogManager.GetLogger("TwoWayTurnLane").SetShowsErrorsInUI(true);
        private TurnLanePrefabSystem? turnLanePrefab;
        private TurnLanePathSystem? turnLanePath;
        private TurnLaneControlSystem? trafficControl;

        public void OnLoad(UpdateSystem updateSystem)
        {
            Log.Info("Registering the Road Builder shared turn lane and live traffic controller.");
            // Road Builder registers its saved road prefabs from MainLoop after
            // generation data exists.
            // AddPrefab creates Created entities immediately. Run before PrefabSystem so its
            // same-frame PrefabUpdate lets Road Builder discover them before marker cleanup.
            updateSystem.UpdateBefore<TurnLanePrefabSystem, PrefabSystem>(SystemUpdatePhase.MainLoop);
            updateSystem.UpdateAfter<TurnLanePathSystem, Game.Pathfind.LaneDataSystem>(SystemUpdatePhase.ModificationEnd);
            updateSystem.UpdateBefore<TurnLanePathSystem, Game.Pathfind.LanesModifiedSystem>(SystemUpdatePhase.ModificationEnd);
            updateSystem.UpdateAfter<TurnLaneControlSystem, Game.Simulation.CarNavigationSystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateBefore<TurnLaneControlSystem, Game.Simulation.CarMoveSystem>(SystemUpdatePhase.GameSimulation);
            turnLanePrefab = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<TurnLanePrefabSystem>();
            turnLanePath = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<TurnLanePathSystem>();
            trafficControl = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<TurnLaneControlSystem>();
        }

        public void OnDispose()
        {
            if (turnLanePrefab != null) turnLanePrefab.Enabled = false;
            if (turnLanePath != null) turnLanePath.Enabled = false;
            if (trafficControl != null) trafficControl.Enabled = false;
        }
    }
}
