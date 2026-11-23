using System;
using System.Collections.Generic;
using Game.City;
using RoadBuilder.Domain.Configurations;
using Twtl.Core;
using Twtl.Mod.RoadBuilderIntegration;
using Unity.Entities;

namespace Twtl.Mod.Diagnostics
{
    /// <summary>
    /// Read-only bridge from a narrowly recognized Road Builder layout to the
    /// core cross-section validator. Unknown sections stay unknown; this never
    /// enables traffic or asserts that the placed network is safe.
    /// </summary>
    internal static class RoadBuilderCrossSectionDiagnostics
    {
        private const string OrdinaryCarSection = "Car Drive Section 3";

        public static void Write(World world, INetworkConfig config)
        {
            var strips = new List<RoadStrip>(config.Lanes.Count);
            var unknown = new List<string>();
            int centerCount = 0;
            for (int i = 0; i < config.Lanes.Count; i++)
            {
                var lane = config.Lanes[i];
                if (!string.IsNullOrEmpty(lane.GroupPrefabName))
                {
                    unknown.Add($"{i}:group:{lane.GroupPrefabName}");
                    continue;
                }
                if (lane.SectionPrefabName == OrdinaryCarSection)
                {
                    strips.Add(new RoadStrip(StripKind.Travel,
                        lane.Invert ? Direction.Backward : Direction.Forward));
                }
                else if (lane.SectionPrefabName == VirtualLaneGraphFixtureSystem.PrefabName)
                {
                    centerCount++;
                    strips.Add(new RoadStrip(StripKind.SharedTurn));
                }
                else
                {
                    unknown.Add($"{i}:{lane.SectionPrefabName ?? "missing"}");
                }
            }

            if (unknown.Count != 0)
            {
                Mod.Log.Info($"[CROSS-SECTION] recognized={strips.Count} total={config.Lanes.Count} unknown={unknown.Count} firstUnknown={string.Join(",", unknown.GetRange(0, Math.Min(unknown.Count, 8)))}; validation withheld");
                return;
            }

            bool leftHand = world.GetOrCreateSystemManaged<CityConfigurationSystem>().leftHandTraffic;
            var handedness = leftHand ? Handedness.Left : Handedness.Right;
            var result = RoadValidation.Configuration(strips, handedness);
            Mod.Log.Info($"[CROSS-SECTION] road={config.ID} centerSections={centerCount} ordinarySections={strips.Count - centerCount} leftHandTraffic={leftHand} configurationStatus={result.Status} reasons={string.Join(" | ", result.Reasons)}; recognized layout only, runtime access, routing, overlaps and traffic safety unverified");
        }
    }
}
