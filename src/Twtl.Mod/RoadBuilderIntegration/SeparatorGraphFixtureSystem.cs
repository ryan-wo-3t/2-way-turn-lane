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
    /// Disconnected graph experiment. A zero-width native traffic divider after
    /// the center lane should end its contiguous master/slave group. The divider
    /// may also prevent lateral entry, so this is not a functional road design.
    /// </summary>
    public partial class SeparatorGraphFixtureSystem : GameSystemBase
    {
        public const string PrefabName = "TWTL ZERO-WIDTH SEPARATOR GRAPH TEST 3m";
        public const string RoadId = "TWTL_SEPARATOR_GRAPH_TEST_ROAD_V1";
        private const string NativeSectionName = "Invisible Car Twoway Section 3";
        private const string NativeLaneName = "Invisible Car Twoway Lane 3";
        private const string DividerSectionName = "Road Median 0";
        private const string SideLaneName = "Car Drive Section 3";
        private bool registered;
        private bool roadAttempted;
        private bool failed;

        protected override void OnUpdate()
        {
            if (failed || Mod.Options?.ShowSeparatorGraphFixture != true) return;
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
                !catalogue.NetSections.TryGetValue(DividerSectionName, out var divider) ||
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

                NetPiecePrefab? blockingPiece = null;
                if (divider.m_Pieces != null)
                {
                    foreach (var info in divider.m_Pieces)
                    {
                        var piece = info.m_Piece;
                        if (piece == null || Math.Abs(piece.m_Width) > 0.01f ||
                            !piece.TryGet<NetDividerPiece>(out var marker) || !marker.m_BlockTraffic ||
                            piece.TryGet<NetPieceLanes>(out var pieceLanes) &&
                            pieceLanes.m_Lanes != null && pieceLanes.m_Lanes.Length != 0) continue;
                        blockingPiece = piece;
                        break;
                    }
                }
                if (blockingPiece == null)
                    throw new InvalidOperationException("No lane-free zero-width native traffic divider was found.");

                var section = ScriptableObject.CreateInstance<NetSectionPrefab>();
                section.name = PrefabName;
                section.m_SubSections = Array.Empty<NetSubSectionInfo>();
                section.m_Pieces = new NetPieceInfo[native.m_Pieces.Length + 1];
                Array.Copy(native.m_Pieces, section.m_Pieces, native.m_Pieces.Length);
                section.m_Pieces[native.m_Pieces.Length] = new NetPieceInfo
                {
                    m_Piece = blockingPiece,
                    m_RequireAll = Array.Empty<NetPieceRequirements>(),
                    m_RequireAny = Array.Empty<NetPieceRequirements>(),
                    m_RequireNone = Array.Empty<NetPieceRequirements>()
                };
                var laneInfo = section.AddOrGetComponent<RoadBuilderLaneInfo>();
                laneInfo.RequireAll = RoadCategory.Road;
                laneInfo.RequireAny = RoadCategory.Road;
                laneInfo.RequireNone = RoadCategory.NonRoad;
                laneInfo.NoDirection = true;
                if (!World.GetOrCreateSystemManaged<PrefabSystem>().AddPrefab(section))
                    throw new InvalidOperationException("PrefabSystem rejected separator graph section.");
                registered = true;
                Mod.Log.Warn($"[SEPARATOR GRAPH TEST] Registered opt-in center section with native zero-width blocker {blockingPiece.name}. Disconnected graph inspection only; blocker may prevent lateral entry.");
            }
            catch (Exception error)
            {
                failed = true;
                Mod.Log.Error(error, "Separator graph fixture registration refused.");
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
                    Name = "TWTL SEPARATOR GRAPH TEST - DISCONNECTED ONLY",
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
                    throw new InvalidOperationException("Road Builder rejected separator graph road.");
                Mod.Log.Warn("[SEPARATOR GRAPH TEST] Registered disconnected-only graph test road. No car trial or traffic safeguards.");
            }
            catch (Exception error)
            {
                Mod.Log.Error(error, "Separator graph test road registration failed.");
            }
        }
    }
}
