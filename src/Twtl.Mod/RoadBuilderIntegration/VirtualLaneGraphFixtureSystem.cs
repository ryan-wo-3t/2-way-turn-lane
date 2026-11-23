using System;
using System.Collections.Generic;
using Game;
using Game.Prefabs;
using RoadBuilder.Domain.Components.Prefabs;
using RoadBuilder.Domain.Configurations;
using RoadBuilder.Domain.Enums;
using RoadBuilder.Systems;
using UnityEngine;

namespace Twtl.Mod.RoadBuilderIntegration
{
    /// <summary>
    /// Disconnected graph experiment. A zero-width, non-road lane should break
    /// composition lane grouping without the carriageway barrier created by
    /// NetPieceFlags.BlockTraffic. No traffic may use this fixture yet.
    /// </summary>
    public partial class VirtualLaneGraphFixtureSystem : GameSystemBase
    {
        public const string PrefabName = "TWTL VIRTUAL-LANE GRAPH TEST 3m";
        public const string RoadId = "TWTL_VIRTUAL_LANE_GRAPH_TEST_ROAD_V1";
        private const string NativeSectionName = "Invisible Car Twoway Section 3";
        private const string NativeLaneName = "Invisible Car Twoway Lane 3";
        private const string VirtualLaneName = "TWTL ZERO-WIDTH NON-ROAD LANE GRAPH TEST";
        private const string VirtualPieceName = "TWTL ZERO-WIDTH NON-ROAD PIECE GRAPH TEST";
        private const string SideLaneName = "Car Drive Section 3";
        private bool registered;
        private bool roadAttempted;
        private bool failed;

        protected override void OnUpdate()
        {
            if (failed || Mod.Options?.ShowVirtualLaneGraphFixture != true) return;
            var catalogue = World.GetExistingSystemManaged<RoadBuilderGenerationDataSystem>();
            if (registered)
            {
                if (!roadAttempted && catalogue != null &&
                    catalogue.NetSections.ContainsKey(PrefabName) &&
                    catalogue.NetSections.ContainsKey(SideLaneName))
                {
                    roadAttempted = true;
                    RegisterTestRoad();
                }
                return;
            }
            if (catalogue?.RoadGenerationData == null ||
                !catalogue.NetSections.TryGetValue(NativeSectionName, out var native) ||
                !catalogue.NetLanes.TryGetValue(NativeLaneName, out var expectedLane)) return;

            try
            {
                if ((native.m_SubSections?.Length ?? 0) != 0 ||
                    native.m_Pieces == null || native.m_Pieces.Length == 0)
                    throw new InvalidOperationException("Native center section has nested sections or no pieces.");
                int lanePieces = 0;
                foreach (var info in native.m_Pieces)
                {
                    var piece = info.m_Piece;
                    if (piece == null) throw new InvalidOperationException("Native center piece is missing.");
                    if (!piece.TryGet<NetPieceLanes>(out var lanes)) continue;
                    if (lanes.m_Lanes == null || lanes.m_Lanes.Length != 1 ||
                        lanes.m_Lanes[0].m_Lane != expectedLane)
                        throw new InvalidOperationException("Native center piece has an unexpected lane.");
                    lanePieces++;
                }
                if (lanePieces == 0 || !expectedLane.TryGet<CarLane>(out var car) ||
                    !car.m_Twoway || Math.Abs(car.m_Width - 3f) > 0.01f)
                    throw new InvalidOperationException("Native center is not the verified 3 m two-way lane.");

                var prefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
                var virtualLane = ScriptableObject.CreateInstance<NetLanePrefab>();
                virtualLane.name = VirtualLaneName;
                // No CarLane, TrackLane, PedestrianLane, ParkingLane or pathfind
                // component. Installed NetInitializeSystem gives this lane no
                // Road flag or drivable path edge.
                if (!prefabSystem.AddPrefab(virtualLane))
                    throw new InvalidOperationException("PrefabSystem rejected virtual test lane.");

                var virtualPiece = ScriptableObject.CreateInstance<NetPiecePrefab>();
                virtualPiece.name = VirtualPieceName;
                virtualPiece.m_Width = 0f;
                virtualPiece.AddOrGetComponent<NetPieceLanes>().m_Lanes = new[]
                {
                    new NetLaneInfo { m_Lane = virtualLane }
                };
                if (!prefabSystem.AddPrefab(virtualPiece))
                    throw new InvalidOperationException("PrefabSystem rejected virtual test piece.");

                var section = ScriptableObject.CreateInstance<NetSectionPrefab>();
                section.name = PrefabName;
                section.m_SubSections = Array.Empty<NetSubSectionInfo>();
                section.m_Pieces = new NetPieceInfo[native.m_Pieces.Length + 1];
                Array.Copy(native.m_Pieces, section.m_Pieces, native.m_Pieces.Length);
                section.m_Pieces[native.m_Pieces.Length] = new NetPieceInfo
                {
                    m_Piece = virtualPiece,
                    m_RequireAll = Array.Empty<NetPieceRequirements>(),
                    m_RequireAny = Array.Empty<NetPieceRequirements>(),
                    m_RequireNone = Array.Empty<NetPieceRequirements>()
                };
                var laneInfo = section.AddOrGetComponent<RoadBuilderLaneInfo>();
                laneInfo.RequireAll = RoadCategory.Road;
                laneInfo.RequireAny = RoadCategory.Road;
                laneInfo.RequireNone = RoadCategory.NonRoad;
                laneInfo.NoDirection = true;
                if (!prefabSystem.AddPrefab(section))
                    throw new InvalidOperationException("PrefabSystem rejected virtual-lane graph section.");
                registered = true;
                Mod.Log.Warn("[VIRTUAL LANE GRAPH TEST] Registered zero-width non-road separator section. Disconnected graph inspection only; lateral access and safety unknown.");
            }
            catch (Exception error)
            {
                failed = true;
                Mod.Log.Error(error, "Virtual-lane graph fixture registration refused.");
            }
        }

        private void RegisterTestRoad()
        {
            try
            {
                var builder = World.GetExistingSystemManaged<RoadBuilderSystem>();
                if (builder == null) throw new InvalidOperationException("Road Builder road system is unavailable.");
                if (builder.Configurations.ContainsKey(RoadId)) return;
                var config = new RoadConfig
                {
                    ID = RoadId,
                    Name = "TWTL VIRTUAL-LANE GRAPH TEST - DISCONNECTED ONLY",
                    Version = RoadBuilderSerializeSystem.CURRENT_VERSION,
                    Category = RoadCategory.Road,
                    SpeedLimit = 80f,
                    ToolbarState = ShowInToolbarState.Show,
                    Lanes = new List<LaneConfig>
                    {
                        new LaneConfig { SectionPrefabName = SideLaneName, Invert = true },
                        new LaneConfig { SectionPrefabName = PrefabName },
                        new LaneConfig { SectionPrefabName = SideLaneName }
                    }
                };
                if (builder.AddPrefab(config) == null)
                    throw new InvalidOperationException("Road Builder rejected virtual-lane graph road.");
                Mod.Log.Warn("[VIRTUAL LANE GRAPH TEST] Registered disconnected-only road. No car trial or traffic safeguards.");
            }
            catch (Exception error)
            {
                Mod.Log.Error(error, "Virtual-lane graph test road registration failed.");
            }
        }
    }
}
