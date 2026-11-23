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
    /// Disconnected graph experiment: does a zero-barrier median designation keep
    /// the native two-way car lane out of an ordinary car-lane master group?
    /// </summary>
    public partial class MedianGraphFixtureSystem : GameSystemBase
    {
        public const string PrefabName = "TWTL MEDIAN-FLAG GRAPH TEST 3m";
        public const string RoadId = "TWTL_MEDIAN_GRAPH_TEST_ROAD_V1";
        private const string SourceName = "Invisible Car Twoway Section 3";
        private const string LaneName = "Invisible Car Twoway Lane 3";
        private const string SideLaneName = "Car Drive Section 3";
        private bool registered;
        private bool discovered;
        private bool roadAttempted;
        private bool failed;

        protected override void OnUpdate()
        {
            if (failed || Mod.Options?.ShowMedianGraphFixture != true) return;
            var catalogue = World.GetExistingSystemManaged<RoadBuilderGenerationDataSystem>();
            if (registered)
            {
                if (!discovered && catalogue != null && catalogue.NetSections.ContainsKey(PrefabName))
                {
                    discovered = true;
                    Mod.Log.Info("[MEDIAN GRAPH TEST] Road Builder discovered median-flag section.");
                }
                if (discovered && !roadAttempted && catalogue != null && catalogue.NetSections.ContainsKey(SideLaneName))
                {
                    roadAttempted = true;
                    RegisterTestRoad();
                }
                return;
            }
            if (catalogue?.RoadGenerationData == null ||
                !catalogue.NetSections.TryGetValue(SourceName, out var source) ||
                !catalogue.NetLanes.TryGetValue(LaneName, out var expectedLane)) return;

            try
            {
                if ((source.m_SubSections?.Length ?? 0) != 0 ||
                    source.m_Pieces == null || source.m_Pieces.Length == 0)
                    throw new InvalidOperationException("Native source has nested sections or no pieces.");
                int matched = 0;
                foreach (var pieceInfo in source.m_Pieces)
                {
                    var piece = pieceInfo.m_Piece;
                    if (piece == null) throw new InvalidOperationException("Native source has a missing piece.");
                    if (!piece.TryGet<NetPieceLanes>(out var pieceLanes)) continue;
                    if (pieceLanes.m_Lanes == null || pieceLanes.m_Lanes.Length != 1 ||
                        pieceLanes.m_Lanes[0].m_Lane != expectedLane)
                        throw new InvalidOperationException($"Native piece {piece.name} contains unexpected lanes.");
                    matched++;
                }
                if (matched == 0 || !expectedLane.TryGet<CarLane>(out var car) ||
                    !car.m_Twoway || Math.Abs(car.m_Width - 3f) > 0.01f)
                    throw new InvalidOperationException("Native source is not the verified 3 m two-way car lane.");

                var section = ScriptableObject.CreateInstance<NetSectionPrefab>();
                section.name = PrefabName;
                section.m_SubSections = Array.Empty<NetSubSectionInfo>();
                section.m_Pieces = (NetPieceInfo[])source.m_Pieces.Clone();
                var info = section.AddOrGetComponent<RoadBuilderLaneInfo>();
                info.RequireAll = RoadCategory.Road;
                info.RequireAny = RoadCategory.Road;
                info.RequireNone = RoadCategory.NonRoad;
                info.NoDirection = true;
                // This changes NetSectionInfo.m_Median in Road Builder's generated
                // network. It does not create a raised barrier or validate turns.
                info.Median = true;
                if (!World.GetOrCreateSystemManaged<PrefabSystem>().AddPrefab(section))
                    throw new InvalidOperationException("PrefabSystem rejected median-flag section.");
                registered = true;
                Mod.Log.Warn("[MEDIAN GRAPH TEST] Registered opt-in median-flag graph fixture. Disconnected road only; no traffic safeguards.");
            }
            catch (Exception error)
            {
                failed = true;
                Mod.Log.Error(error, "Median-flag graph fixture registration refused.");
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
                    Name = "TWTL MEDIAN-FLAG GRAPH TEST - DISCONNECTED ONLY",
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
                    throw new InvalidOperationException("Road Builder rejected median-flag test road.");
                Mod.Log.Warn("[MEDIAN GRAPH TEST] Registered opt-in road for disconnected graph inspection only.");
            }
            catch (Exception error)
            {
                Mod.Log.Error(error, "Median-flag test road registration failed.");
            }
        }
    }
}
