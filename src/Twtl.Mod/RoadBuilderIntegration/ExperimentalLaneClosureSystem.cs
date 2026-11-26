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
        private EntityQuery changedJunctionLanes;
        private Entity lastCity;
        private bool fullScanPending = true;
        private bool unresolvedCenter;
        private bool lastPrototypeEnabled;
        private readonly Dictionary<Entity, bool> unsafeRoadPrefabs = new Dictionary<Entity, bool>();
        private readonly Dictionary<Entity, bool> drivableRoadPrefabs = new Dictionary<Entity, bool>();

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
            changedJunctionLanes = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<Lane>(), ComponentType.ReadOnly<Game.Net.CarLane>(),
                    ComponentType.ReadOnly<NodeLane>() },
                Any = new[] { ComponentType.ReadOnly<Created>(), ComponentType.ReadOnly<Updated>() },
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
                drivableRoadPrefabs.Clear();
            }
            bool prototypeEnabled = Mod.Options?.EnableDrivablePrototype == true;
            if (prototypeEnabled != lastPrototypeEnabled)
            {
                lastPrototypeEnabled = prototypeEnabled;
                fullScanPending = true;
                unsafeRoadPrefabs.Clear();
                drivableRoadPrefabs.Clear();
            }
            if (World.GetExistingSystemManaged<PrefabSystem>() == null) return;
            // A road edit can regenerate its junction connectors without also
            // marking the center edge lane as changed. Re-evaluate center
            // endpoints whenever new node lanes appear in the opt-in trial.
            bool junctionChanged = prototypeEnabled && !changedJunctionLanes.IsEmptyIgnoreFilter;
            if (!fullScanPending && changedLanes.IsEmptyIgnoreFilter && !junctionChanged) return;

            EntityQuery query = fullScanPending || junctionChanged ? allLanes : changedLanes;
            using (var lanes = query.ToEntityArray(Allocator.Temp))
            {
                if (fullScanPending && lanes.Length == 0) return;
                unsafeRoadPrefabs.Clear();
                drivableRoadPrefabs.Clear();
                unresolvedCenter = false;
                int closed = 0;
                int gatedJunctions = 0;
                int uncertainPrototypesClosed = 0;
                var visitedNodeLanes = new HashSet<Entity>();
                for (int i = 0; i < lanes.Length; i++)
                {
                    Entity lane = lanes[i];
                    if (IsDrivablePrototypeCenter(lane))
                    {
                        var gate = GateJunctionConnections(lane, visitedNodeLanes);
                        gatedJunctions += gate.Closed;
                        if (!gate.StartMatched || !gate.EndMatched)
                        {
                            if (CloseLane(lane)) uncertainPrototypesClosed++;
                            Mod.Log.Warn($"[SAFETY] V3 center {lane} lacks a confirmed junction connector at one or both ends; center path closed instead of assuming safe routing.");
                        }
                        else if (AddForbiddenRouteCost(lane))
                            Mod.Log.Info($"[JUNCTION-GATE] V3 center={lane} uses the native Forbidden route cost; this is a preference, not a turn-only prohibition.");
                        continue;
                    }
                    if (!IsExperimentalCenter(lane)) continue;
                    if (CloseLane(lane)) closed++;
                }
                if (closed != 0)
                    Mod.Log.Warn($"[SAFETY] Closed {closed} experimental center lanes to native pathfinding; ordinary lanes remain open. This is not a functional turn lane.");
                if (gatedJunctions != 0)
                    Mod.Log.Warn($"[SAFETY] Closed {gatedJunctions} V3 center-to-junction connector lanes. Ordinary lanes remain open; roadside turn access and conflict safety are unverified.");
                if (uncertainPrototypesClosed != 0)
                    Mod.Log.Warn($"[SAFETY] Closed {uncertainPrototypesClosed} V3 center lanes because the junction gate could not establish both endpoints.");
                fullScanPending = unresolvedCenter;
            }
        }

        private bool CloseLane(Entity lane)
        {
            var car = EntityManager.GetComponentData<Game.Net.CarLane>(lane);
            if (car.m_BlockageStart == 0 && car.m_BlockageEnd == byte.MaxValue) return false;
            car.m_BlockageStart = 0;
            car.m_BlockageEnd = byte.MaxValue;
            EntityManager.SetComponentData(lane, car);
            if (!EntityManager.HasComponent<Created>(lane) &&
                !EntityManager.HasComponent<Updated>(lane) &&
                !EntityManager.HasComponent<PathfindUpdated>(lane))
                EntityManager.AddComponent<PathfindUpdated>(lane);
            return true;
        }

        private bool AddForbiddenRouteCost(Entity lane)
        {
            var car = EntityManager.GetComponentData<Game.Net.CarLane>(lane);
            if ((car.m_Flags & CarLaneFlags.Forbidden) != 0) return false;
            car.m_Flags |= CarLaneFlags.Forbidden;
            EntityManager.SetComponentData(lane, car);
            if (!EntityManager.HasComponent<Created>(lane) &&
                !EntityManager.HasComponent<Updated>(lane) &&
                !EntityManager.HasComponent<PathfindUpdated>(lane))
                EntityManager.AddComponent<PathfindUpdated>(lane);
            return true;
        }

        private struct JunctionGateResult
        {
            public int Closed;
            public bool StartMatched;
            public bool EndMatched;
        }

        private bool IsDrivablePrototypeCenter(Entity lane)
        {
            if (Mod.Options?.EnableDrivablePrototype != true) return false;
            var car = EntityManager.GetComponentData<Game.Net.CarLane>(lane);
            if ((car.m_Flags & CarLaneFlags.Twoway) == 0 ||
                !EntityManager.HasComponent<PrefabRef>(lane)) return false;
            var prefabSystem = World.GetExistingSystemManaged<PrefabSystem>();
            var laneRef = EntityManager.GetComponentData<PrefabRef>(lane);
            if (!prefabSystem.TryGetSpecificPrefab<NetLanePrefab>(laneRef, out var lanePrefab) ||
                lanePrefab.name != NativeCenterLane) return false;
            Entity owner = EntityManager.GetComponentData<Owner>(lane).m_Owner;
            if (!EntityManager.Exists(owner) || !EntityManager.HasComponent<PrefabRef>(owner)) return false;
            Entity roadPrefabEntity = EntityManager.GetComponentData<PrefabRef>(owner).m_Prefab;
            if (drivableRoadPrefabs.TryGetValue(roadPrefabEntity, out bool drivable)) return drivable;
            var roadRef = new PrefabRef(roadPrefabEntity);
            drivable = prefabSystem.TryGetSpecificPrefab<NetGeometryPrefab>(roadRef, out var road) &&
                road is INetworkBuilderPrefab built &&
                built.Config?.ID == VirtualLaneGraphFixtureSystem.DrivableRoadId;
            drivableRoadPrefabs[roadPrefabEntity] = drivable;
            return drivable;
        }

        private JunctionGateResult GateJunctionConnections(Entity centerEntity, HashSet<Entity> visited)
        {
            var result = new JunctionGateResult();
            Entity owner = EntityManager.GetComponentData<Owner>(centerEntity).m_Owner;
            if (!EntityManager.HasComponent<Game.Net.Edge>(owner) ||
                !EntityManager.HasComponent<Lane>(centerEntity)) return result;
            var edge = EntityManager.GetComponentData<Game.Net.Edge>(owner);
            var center = EntityManager.GetComponentData<Lane>(centerEntity);
            GateNode(edge.m_Start, center, visited, ref result);
            if (edge.m_End != edge.m_Start) GateNode(edge.m_End, center, visited, ref result);
            return result;
        }

        private void GateNode(Entity node, Lane center, HashSet<Entity> visited, ref JunctionGateResult result)
        {
            if (!EntityManager.Exists(node) || !EntityManager.HasBuffer<Game.Net.SubLane>(node)) return;
            var subLanes = EntityManager.GetBuffer<Game.Net.SubLane>(node, true);
            for (int i = 0; i < subLanes.Length; i++)
            {
                Entity connector = subLanes[i].m_SubLane;
                if (!EntityManager.Exists(connector) ||
                    !EntityManager.HasComponent<NodeLane>(connector) ||
                    !EntityManager.HasComponent<Lane>(connector) ||
                    !EntityManager.HasComponent<Game.Net.CarLane>(connector) ||
                    EntityManager.HasComponent<Deleted>(connector) ||
                    EntityManager.HasComponent<Game.Tools.Temp>(connector)) continue;
                var path = EntityManager.GetComponentData<Lane>(connector);
                bool startMatch = path.m_StartNode.Equals(center.m_StartNode) ||
                    path.m_EndNode.Equals(center.m_StartNode);
                bool endMatch = path.m_StartNode.Equals(center.m_EndNode) ||
                    path.m_EndNode.Equals(center.m_EndNode);
                if (!startMatch && !endMatch) continue;
                result.StartMatched |= startMatch;
                result.EndMatched |= endMatch;
                if (!visited.Add(connector)) continue;
                if (CloseLane(connector))
                {
                    result.Closed++;
                    Mod.Log.Info($"[JUNCTION-GATE] node={node} connector={connector}; native full blockage at a V3 center endpoint.");
                }
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
