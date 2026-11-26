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
    /// Registers the shared Road Builder lane. The zero-width, non-road lane
    /// prevents the game from merging its bidirectional path edge into an
    /// adjacent one-way master lane. Legacy names/IDs keep Bonham loadable.
    /// </summary>
    public partial class TurnLanePrefabSystem : GameSystemBase
    {
        public const string PrefabName = "TWTL VIRTUAL-LANE GRAPH TEST 3m";
        public const string DrivableRoadId = "TWTL_DRIVABLE_FIVE_LANE_SIDEWALK_TEST_ROAD_V3";
        private const string NativeSectionName = "Invisible Car Twoway Section 3";
        private const string NativeLaneName = "Invisible Car Twoway Lane 3";
        private const string VirtualLaneName = "TWTL ZERO-WIDTH NON-ROAD LANE GRAPH TEST";
        private const string VirtualPieceName = "TWTL ZERO-WIDTH NON-ROAD PIECE GRAPH TEST";
        private const string SideLaneName = "Car Drive Section 3";
        private const string SidewalkSectionName = "Sidewalk With Parking 6";
        private const RoadAddons RequiredAddons = RoadAddons.HasUndergroundWaterPipes |
            RoadAddons.HasUndergroundElectricityCable | RoadAddons.GeneratesZoningBlocks;
        private bool registered;
        private bool drivableRoadAttempted;
        private bool failed;

        protected override void OnUpdate()
        {
            if (failed) return;
            var catalogue = World.GetExistingSystemManaged<RoadBuilderGenerationDataSystem>();
            if (registered)
            {
                if (catalogue != null &&
                    catalogue.NetSections.ContainsKey(PrefabName) &&
                    catalogue.NetSections.ContainsKey(SideLaneName) &&
                    catalogue.NetSections.ContainsKey(SidewalkSectionName))
                {
                    if (!drivableRoadAttempted)
                    {
                        drivableRoadAttempted = true;
                        RegisterRoad();
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
                    throw new InvalidOperationException("PrefabSystem rejected shared-lane separator.");

                var virtualPiece = ScriptableObject.CreateInstance<NetPiecePrefab>();
                virtualPiece.name = VirtualPieceName;
                virtualPiece.m_Width = 0f;
                virtualPiece.AddOrGetComponent<NetPieceLanes>().m_Lanes = new[]
                {
                    new NetLaneInfo { m_Lane = virtualLane }
                };
                if (!prefabSystem.AddPrefab(virtualPiece))
                    throw new InvalidOperationException("PrefabSystem rejected shared-lane separator piece.");

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
                Mod.Log.Info("Registered Road Builder shared center section with an independent two-way graph lane.");
            }
            catch (Exception error)
            {
                failed = true;
                Mod.Log.Error(error, "Shared turn-lane section registration refused.");
            }
        }

        private void RegisterRoad()
        {
            try
            {
                var builder = World.GetExistingSystemManaged<RoadBuilderSystem>();
                if (builder == null) throw new InvalidOperationException("Road Builder road system is unavailable.");
                if (builder.Configurations.TryGetValue(DrivableRoadId, out var existing))
                {
                    var existingAddons = existing.Config?.Addons ?? 0;
                    if ((existingAddons & RequiredAddons) != RequiredAddons)
                        Mod.Log.Warn("Existing saved turn-lane road lacks utility/zoning addons; leaving its configuration untouched.");
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
                    new LaneConfig { SectionPrefabName = SidewalkSectionName, Invert = true },
                    new LaneConfig { SectionPrefabName = SideLaneName, Invert = true },
                    new LaneConfig { SectionPrefabName = SideLaneName, Invert = true },
                    new LaneConfig { SectionPrefabName = PrefabName },
                    new LaneConfig { SectionPrefabName = SideLaneName },
                    new LaneConfig { SectionPrefabName = SideLaneName },
                    new LaneConfig { SectionPrefabName = SidewalkSectionName }
                };
                var config = new RoadConfig
                {
                    ID = DrivableRoadId,
                    Name = "TWTL Five Lane Road",
                    Version = RoadBuilderSerializeSystem.CURRENT_VERSION,
                    Category = RoadCategory.Road,
                    Addons = RequiredAddons,
                    SpeedLimit = 80f,
                    ToolbarState = ShowInToolbarState.Show,
                    Lanes = roadLanes
                };
                if (builder.AddPrefab(config) == null)
                    throw new InvalidOperationException("Road Builder rejected virtual-lane graph road.");
                Mod.Log.Info("Registered Road Builder five-lane road; saved V3 road ID retained.");
            }
            catch (Exception error)
            {
                Mod.Log.Error(error, "Turn-lane road registration failed.");
            }
        }
    }
}
