using System;
using System.Collections.Generic;
using Game;
using Game.City;
using Game.Common;
using Game.Net;
using Game.Pathfind;
using Game.Prefabs;
using Game.Simulation;
using RoadBuilder.Domain.Prefabs;
using RoadBuilder.Utilities;
using Unity.Collections;
using Unity.Entities;

namespace Twtl.Mod.RoadBuilderIntegration
{
    /// <summary>
    /// Closes the path graph on this add-on's unsafe experimental center lanes.
    /// The native pathfinder treats a complete blockage as a hard denial. Run
    /// after vanilla recalculates lane data and before it updates graph edges.
    /// </summary>
    public partial class ExperimentalLaneClosureSystem : GameSystemBase
    {
        private const string NativeCenterLane = "Invisible Car Twoway Lane 3";
        private EntityQuery allLanes;
        private EntityQuery changedLanes;
        private Entity lastCity;
        private bool fullScanPending = true;
        private bool unresolvedCenter;
        private bool lastPrototypeEnabled;
        private readonly Dictionary<Entity, bool> unsafeRoadPrefabs = new Dictionary<Entity, bool>();

        protected override void OnCreate()
        {
            base.OnCreate();
            var all = new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<Lane>(), ComponentType.ReadOnly<Game.Net.CarLane>(),
                    ComponentType.ReadOnly<EdgeLane>(), ComponentType.ReadOnly<Owner>() },
                None = new[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Game.Tools.Temp>() }
            };
            allLanes = GetEntityQuery(all);
            changedLanes = GetEntityQuery(new EntityQueryDesc
            {
                All = all.All,
                Any = new[] { ComponentType.ReadOnly<Created>(), ComponentType.ReadOnly<Updated>(),
                    ComponentType.ReadOnly<PathfindUpdated>() },
                None = all.None
            });
        }

        protected override void OnUpdate()
        {
            Entity city = World.GetExistingSystemManaged<CitySystem>()?.City ?? Entity.Null;
            if (city == Entity.Null) return;
            if (city != lastCity)
            {
                lastCity = city;
                fullScanPending = true;
                unsafeRoadPrefabs.Clear();
            }
            bool prototypeEnabled = Mod.Options?.EnableDrivablePrototype == true;
            if (prototypeEnabled != lastPrototypeEnabled)
            {
                lastPrototypeEnabled = prototypeEnabled;
                fullScanPending = true;
                unsafeRoadPrefabs.Clear();
            }
            if (World.GetExistingSystemManaged<PrefabSystem>() == null) return;
            if (!fullScanPending && changedLanes.IsEmptyIgnoreFilter) return;

            EntityQuery query = fullScanPending ? allLanes : changedLanes;
            using (var lanes = query.ToEntityArray(Allocator.Temp))
            {
                if (fullScanPending && lanes.Length == 0) return;
                unsafeRoadPrefabs.Clear();
                unresolvedCenter = false;
                int closed = 0;
                for (int i = 0; i < lanes.Length; i++)
                {
                    Entity lane = lanes[i];
                    if (!IsExperimentalCenter(lane)) continue;
                    var car = EntityManager.GetComponentData<Game.Net.CarLane>(lane);
                    if (car.m_BlockageStart == 0 && car.m_BlockageEnd == byte.MaxValue) continue;
                    car.m_BlockageStart = 0;
                    car.m_BlockageEnd = byte.MaxValue;
                    EntityManager.SetComponentData(lane, car);
                    if (!EntityManager.HasComponent<Created>(lane) &&
                        !EntityManager.HasComponent<Updated>(lane) &&
                        !EntityManager.HasComponent<PathfindUpdated>(lane))
                        EntityManager.AddComponent<PathfindUpdated>(lane);
                    closed++;
                }
                if (closed != 0)
                    Mod.Log.Warn($"[SAFETY] Closed {closed} experimental center lanes to native pathfinding; ordinary lanes remain open. This is not a functional turn lane.");
                fullScanPending = unresolvedCenter;
            }
        }

        private bool IsExperimentalCenter(Entity lane)
        {
            var car = EntityManager.GetComponentData<Game.Net.CarLane>(lane);
            if ((car.m_Flags & CarLaneFlags.Twoway) == 0 &&
                !EntityManager.HasComponent<MasterLane>(lane)) return false;
            if (!EntityManager.HasComponent<PrefabRef>(lane)) return false;
            var prefabSystem = World.GetExistingSystemManaged<PrefabSystem>();
            var laneRef = EntityManager.GetComponentData<PrefabRef>(lane);
            if (!prefabSystem.TryGetSpecificPrefab<NetLanePrefab>(laneRef, out var lanePrefab)) return false;
            if (lanePrefab.name != NativeCenterLane) return false;

            Entity owner = EntityManager.GetComponentData<Owner>(lane).m_Owner;
            if (!EntityManager.Exists(owner) || !EntityManager.HasComponent<PrefabRef>(owner))
            {
                unresolvedCenter = true;
                return false;
            }
            Entity roadPrefabEntity = EntityManager.GetComponentData<PrefabRef>(owner).m_Prefab;
            if (unsafeRoadPrefabs.TryGetValue(roadPrefabEntity, out bool unsafeRoad)) return unsafeRoad;
            var roadRef = new PrefabRef(roadPrefabEntity);
            if (!prefabSystem.TryGetSpecificPrefab<NetGeometryPrefab>(roadRef, out var road))
            {
                unresolvedCenter = true;
                return false;
            }
            if (road is INetworkBuilderPrefab built && built.Config == null)
            {
                unresolvedCenter = true;
                return false;
            }
            unsafeRoad = road is INetworkBuilderPrefab builder && HasExperimentalSection(builder);
            unsafeRoadPrefabs[roadPrefabEntity] = unsafeRoad;
            return unsafeRoad;
        }

        private static bool HasExperimentalSection(INetworkBuilderPrefab built)
        {
            // The separate, explicitly opted-in disposable-city road is the
            // only experiment allowed past the hard pathfinder closure.
            if (built.Config?.ID == VirtualLaneGraphFixtureSystem.DrivableRoadId &&
                Mod.Options?.EnableDrivablePrototype == true) return false;
            var lanes = built.Config?.Lanes;
            if (lanes == null) return false;
            foreach (var lane in lanes)
            {
                string section = lane.SectionPrefabName ?? "";
                if (section == NativeFixtureSystem.PrefabName ||
                    section == MedianGraphFixtureSystem.PrefabName ||
                    section == SeparatorGraphFixtureSystem.PrefabName ||
                    section == VirtualLaneGraphFixtureSystem.PrefabName) return true;
            }
            return false;
        }
    }
}
