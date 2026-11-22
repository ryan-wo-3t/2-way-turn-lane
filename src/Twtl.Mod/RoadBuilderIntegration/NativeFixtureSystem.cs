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
    /// Explicitly opted-in native lane geometry experiment for disconnected roads only.
    /// This does not enforce turning, entry control, or conflict safety.
    /// </summary>
    public partial class NativeFixtureSystem : GameSystemBase
    {
        public const string PrefabName = "TWTL NATIVE TWO-WAY UNSAFE TEST 3m";
        public const string RoadId = "TWTL_NATIVE_UNSAFE_TEST_ROAD_V1";
        private const string SourceName = "Invisible Car Twoway Section 3";
        private const string LaneName = "Invisible Car Twoway Lane 3";
        private const string SideLaneName = "Car Drive Section 3";
        private bool registered;
        private bool discovered;
        private bool failed;
        private bool roadAttempted;

        protected override void OnUpdate()
        {
            if (failed) return;
            var catalogue = World.GetExistingSystemManaged<RoadBuilderGenerationDataSystem>();
            if (registered)
            {
                if (!discovered && catalogue != null && catalogue.NetSections.ContainsKey(PrefabName))
                {
                    discovered = true;
                    Mod.Log.Info("[NATIVE TEST] Road Builder discovered the test section; disconnected placement and generated-lane inspection still required.");
                }
                if (discovered && !roadAttempted && catalogue != null &&
                    catalogue.NetSections.ContainsKey(SideLaneName))
                {
                    roadAttempted = true;
                    RegisterTestRoad();
                }
                return;
            }
            if (Mod.Options?.ShowNativeFixture != true || catalogue?.RoadGenerationData == null ||
                !catalogue.NetSections.TryGetValue(SourceName, out var source) ||
                !catalogue.NetLanes.TryGetValue(LaneName, out var expectedLane)) return;

            try
            {
                if ((source.m_SubSections?.Length ?? 0) != 0 ||
                    source.m_Pieces == null || source.m_Pieces.Length == 0)
                    throw new InvalidOperationException("Native source has nested sections or no inspectable pieces.");

                int matchingPieces = 0;
                foreach (var pieceInfo in source.m_Pieces)
                {
                    var piece = pieceInfo.m_Piece;
                    if (piece == null)
                        throw new InvalidOperationException("Native source has a missing piece.");
                    if (!piece.TryGet<NetPieceLanes>(out var pieceLanes)) continue;
                    if (pieceLanes.m_Lanes == null || pieceLanes.m_Lanes.Length != 1 ||
                        pieceLanes.m_Lanes[0].m_Lane != expectedLane)
                        throw new InvalidOperationException($"Native piece {piece.name} contains an unexpected lane layout.");
                    matchingPieces++;
                }
                if (matchingPieces == 0 || !expectedLane.TryGet<CarLane>(out var car) ||
                    !car.m_Twoway || Math.Abs(car.m_Width - 3f) > 0.01f)
                    throw new InvalidOperationException("Native source is not the verified 3 m two-way car lane.");

                // Build new metadata rather than cloning Road Builder components through
                // Unity JSON; that path failed on its ulong RoadCategory in CS2 1.6.4f1.
                var section = ScriptableObject.CreateInstance<NetSectionPrefab>();
                section.name = PrefabName;
                section.m_SubSections = Array.Empty<NetSubSectionInfo>();
                section.m_Pieces = (NetPieceInfo[])source.m_Pieces.Clone();
                var info = section.AddOrGetComponent<RoadBuilderLaneInfo>();
                info.RequireAll = RoadCategory.Road;
                info.RequireAny = RoadCategory.Road;
                info.RequireNone = RoadCategory.NonRoad;
                info.NoDirection = true;
                info.Median = false;
                if (!World.GetOrCreateSystemManaged<PrefabSystem>().AddPrefab(section))
                    throw new InvalidOperationException("PrefabSystem rejected the native test section.");
                registered = true;
                Mod.Log.Warn($"[NATIVE TEST] Registered {PrefabName} from {SourceName} with {matchingPieces} lane-bearing source pieces. Experimental drivable geometry only; use disconnected roads in disposable cities. No traffic safety or turn-only routing exists.");
            }
            catch (Exception error)
            {
                failed = true;
                Mod.Log.Error(error, "Native test section registration refused; no experimental lane was added.");
            }
        }

        private void RegisterTestRoad()
        {
            try
            {
                var builder = World.GetExistingSystemManaged<RoadBuilderSystem>();
                if (builder == null)
                    throw new InvalidOperationException("Road Builder road system is unavailable.");
                if (builder.Configurations.ContainsKey(RoadId))
                {
                    Mod.Log.Info("[NATIVE TEST] Test road already exists; leaving its saved configuration untouched.");
                    return;
                }

                // A self-contained road prefab removes a UI drag gesture from the geometry
                // experiment. It is still never connected to traffic in the test protocol.
                var config = new RoadConfig
                {
                    ID = RoadId,
                    Name = "TWTL NATIVE UNSAFE TEST ROAD - DISCONNECTED ONLY",
                    Version = RoadBuilderSerializeSystem.CURRENT_VERSION,
                    Category = RoadCategory.Road,
                    // Road Builder's metric options display SpeedLimit / 2 as km/h.
                    SpeedLimit = 80f,
                    ToolbarState = ShowInToolbarState.Show,
                    Lanes = new List<LaneConfig>
                    {
                        new LaneConfig { SectionPrefabName = SideLaneName, Invert = true },
                        new LaneConfig { SectionPrefabName = PrefabName },
                        new LaneConfig { SectionPrefabName = SideLaneName }
                    }
                };
                var road = builder.AddPrefab(config);
                if (road == null)
                    throw new InvalidOperationException("Road Builder rejected the preconfigured native test road.");
                Mod.Log.Warn($"[NATIVE TEST] Registered opt-in road {RoadId} with one native center section and two ordinary car sections. Place only as a disconnected segment in a disposable city; no route or conflict safety exists.");
            }
            catch (Exception error)
            {
                Mod.Log.Error(error, "Native test road registration failed; the section may still be available in the lane picker.");
            }
        }
    }
}
