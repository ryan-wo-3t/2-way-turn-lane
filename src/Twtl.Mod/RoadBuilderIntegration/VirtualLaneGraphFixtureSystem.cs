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
    /// A zero-width, non-road lane breaks composition grouping without a
    /// carriageway barrier. The separate opt-in road uses this same section
    /// for a disposable-city vehicle trial; it has no traffic safety policy.
    /// </summary>
    public partial class VirtualLaneGraphFixtureSystem : GameSystemBase
    {
        public const string PrefabName = "TWTL VIRTUAL-LANE GRAPH TEST 3m";
        public const string RoadId = "TWTL_VIRTUAL_LANE_GRAPH_TEST_ROAD_V1";
        public const string DrivableRoadId = "TWTL_DRIVABLE_FIVE_LANE_TEST_ROAD_V2";
        private const string NativeSectionName = "Invisible Car Twoway Section 3";
        private const string NativeLaneName = "Invisible Car Twoway Lane 3";
        private const string VirtualLaneName = "TWTL ZERO-WIDTH NON-ROAD LANE GRAPH TEST";
        private const string VirtualPieceName = "TWTL ZERO-WIDTH NON-ROAD PIECE GRAPH TEST";
        private const string SideLaneName = "Car Drive Section 3";
        private const RoadAddons RequiredAddons = RoadAddons.HasUndergroundWaterPipes |
            RoadAddons.HasUndergroundElectricityCable | RoadAddons.GeneratesZoningBlocks;
        private bool registered;
        private bool roadAttempted;
        private bool drivableRoadAttempted;
        private bool failed;

        protected override void OnUpdate()
        {
            if (failed || Mod.Options?.ShowVirtualLaneGraphFixture != true) return;
            var catalogue = World.GetExistingSystemManaged<RoadBuilderGenerationDataSystem>();
            if (registered)
            {
                if (catalogue != null &&
                    catalogue.NetSections.ContainsKey(PrefabName) &&
                    catalogue.NetSections.ContainsKey(SideLaneName))
                {
                    if (!roadAttempted)
                    {
                        roadAttempted = true;
                        RegisterTestRoad(RoadId, "TWTL VIRTUAL-LANE GRAPH TEST - DISCONNECTED ONLY");
                    }
                    if (!drivableRoadAttempted && Mod.Options?.EnableDrivablePrototype == true)
                    {
                        drivableRoadAttempted = true;
                        RegisterTestRoad(DrivableRoadId, "TWTL FIVE-LANE DRIVABLE PROTOTYPE - TEST CITY ONLY");
                    }
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

        private void RegisterTestRoad(string roadId, string roadName)
        {
            try
            {
                var builder = World.GetExistingSystemManaged<RoadBuilderSystem>();
                if (builder == null) throw new InvalidOperationException("Road Builder road system is unavailable.");
                if (builder.Configurations.TryGetValue(roadId, out var existing))
                {
                    var existingAddons = existing.Config?.Addons ?? 0;
                    if ((existingAddons & RequiredAddons) != RequiredAddons)
                        Mod.Log.Warn("[VIRTUAL LANE GRAPH TEST] Existing saved helper road lacks utility/zoning addons; leaving the saved configuration untouched.");
                    return;
                }
                var generation = World.GetExistingSystemManaged<RoadBuilderGenerationDataSystem>()?.RoadGenerationData;
                if (generation == null || generation.ZoneBlockPrefab == null)
                    throw new InvalidOperationException("Road Builder zoning or generation data is unavailable.");
                // These are the exact section keys used by Road Builder 0.8.0's
                // underground water/electricity generation. Refuse the helper
                // road if its utility dependencies are incomplete.
                foreach (string name in new[]
                {
                    "Sewage Pipe Section 1.5", "Pipeline Spacing Section 1",
                    "Ground Cable Section 1", "Water Pipe Section 1",
                    "Stormwater Pipe Section 1.5"
                })
                    if (!generation.NetSectionPrefabs.ContainsKey(name))
                        throw new InvalidOperationException("Road Builder utility section missing: " + name);
                var roadLanes = new List<LaneConfig>
                {
                    new LaneConfig { SectionPrefabName = SideLaneName, Invert = true },
                    new LaneConfig { SectionPrefabName = PrefabName },
                    new LaneConfig { SectionPrefabName = SideLaneName }
                };
                if (roadId == DrivableRoadId)
                {
                    // Match Bonham's four ordinary Lake Street lanes. The
                    // native shared center is the fifth lane.
                    roadLanes.Insert(0, new LaneConfig { SectionPrefabName = SideLaneName, Invert = true });
                    roadLanes.Add(new LaneConfig { SectionPrefabName = SideLaneName });
                }
                var config = new RoadConfig
                {
                    ID = roadId,
                    Name = roadName,
                    Version = RoadBuilderSerializeSystem.CURRENT_VERSION,
                    Category = RoadCategory.Road,
                    Addons = RequiredAddons,
                    SpeedLimit = 80f,
                    ToolbarState = ShowInToolbarState.Show,
                    Lanes = roadLanes
                };
                if (builder.AddPrefab(config) == null)
                    throw new InvalidOperationException("Road Builder rejected virtual-lane graph road.");
                if (roadId == DrivableRoadId)
                    Mod.Log.Warn("[DRIVABLE PROTOTYPE] Registered opt-in five-lane road for disposable-city car trials. Center lane has no turn-only routing, opposing conflict control or safety guarantee. Do not use in valued saves.");
                else
                    Mod.Log.Warn("[VIRTUAL LANE GRAPH TEST] Registered disconnected-only road with Road Builder utility and zoning addons. Native utility continuity is not live-verified; no car trial or traffic safeguards.");
            }
            catch (Exception error)
            {
                Mod.Log.Error(error, "Virtual-lane graph test road registration failed.");
            }
        }
    }
}
