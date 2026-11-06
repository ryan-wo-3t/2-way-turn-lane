using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Colossal.Logging;
using Game.Modding;

namespace Twtl.Mod.Diagnostics
{
    internal static class ApiAudit
    {
        private static readonly string[] Targets =
        {
            "Game.Simulation.CarNavigationSystem", "Game.Simulation.CarLaneSpeedIterator",
            "Game.Net.LaneOverlapSystem", "Game.Net.LaneOverlap", "Game.Net.LaneReservation",
            "Game.Simulation.NetLaneReservationSystem", "Game.Pathfind.PathfindSetupSystem",
            "Game.Simulation.PathfindSetupSystem", "Game.City.CityConfigurationSystem"
        };
        public static void Write(ILog log)
        {
            Assembly game = typeof(IMod).Assembly;
            log.Info($"[API] assembly={game.FullName} MVID={game.ManifestModule.ModuleVersionId}");
            if (!string.IsNullOrEmpty(game.Location))
                log.Info($"[API] fileVersion={FileVersionInfo.GetVersionInfo(game.Location).FileVersion}");
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            foreach (string name in Targets)
            {
                Type? type = game.GetType(name, false);
                if (type == null) { log.Warn("[API] MISSING " + name); continue; }
                log.Info("[API] " + name);
                foreach (var field in type.GetFields(flags).OrderBy(x => x.Name))
                    log.Info($"[API] field {field.FieldType.FullName} {field.Name}");
                foreach (var method in type.GetMethods(flags).Where(x =>
                    x.Name.StartsWith("Check", StringComparison.Ordinal) || x.Name == "UpdateMaxSpeed" ||
                    x.Name == "OnUpdate" || x.Name == "GetPriority").OrderBy(x => x.ToString()))
                    log.Info("[API] method " + method);
            }
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().OrderBy(x => x.GetName().Name))
            {
                string name = assembly.GetName().Name ?? "";
                if (new[] { "RoadBuilder", "Traffic", "AccessAnarchy", "CollisionBeGone", "RoadConstruction", "NetworkTools" }
                    .Any(x => name.IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0))
                    log.Info("[API] integration assembly=" + assembly.FullName);
            }
            log.Warn("[API] Presence and signatures do not verify semantics, ordering, runtime compatibility or collision safety.");
        }
    }
}
