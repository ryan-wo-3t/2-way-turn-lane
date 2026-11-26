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
using Twtl.Core;
using Unity.Collections;
using Unity.Entities;

namespace Twtl.Mod.RoadBuilderIntegration
{
    /// <summary>
    /// Keeps retired research lanes closed and prepares the saved five-lane
    /// road for route-specific control. Run after vanilla lane data refresh
    /// and before native path graph updates.
    /// </summary>
    public partial class TurnLanePathSystem : GameSystemBase
    {
        private const string NativeCenterLane = "Invisible Car Twoway Lane 3";
        private EntityQuery allLanes;
        private EntityQuery changedLanes;
        private EntityQuery changedJunctionLanes;
        private Entity lastCity;
        private bool fullScanPending = true;
        private bool unresolvedCenter;
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
            if (World.GetExistingSystemManaged<PrefabSystem>() == null) return;
            // A road edit can regenerate its junction connectors without also
            // marking the center edge lane as changed. Re-evaluate center
            // endpoints whenever new node lanes appear.
            bool junctionChanged = !changedJunctionLanes.IsEmptyIgnoreFilter;
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
                        else
                        {
                            PrepareControlledLane(lane);
                        }
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

        private void PrepareControlledLane(Entity lane)
        {
            var car = EntityManager.GetComponentData<Game.Net.CarLane>(lane);
            bool changed = false;
            if ((car.m_Flags & CarLaneFlags.Forbidden) != 0)
            {
                car.m_Flags &= ~CarLaneFlags.Forbidden;
                changed = true;
            }
            if (car.m_SpeedLimit > 5f)
            {
                car.m_SpeedLimit = 5f;
                changed = true;
            }
            // Clear only the exact middle blockage applied by our previous
            // prototype. Leave game-created or unknown blockages intact.
            if (EntityManager.HasComponent<Curve>(lane))
            {
                double length = EntityManager.GetComponentData<Curve>(lane).m_Length;
                if (length > 0)
                {
                    Interval middle = TransitCut.Plan(length, new TwtlOptions());
                    byte start = (byte)Math.Floor(255d * middle.Min / length);
                    byte end = (byte)Math.Ceiling(255d * middle.Max / length);
                    if (car.m_BlockageStart == start && car.m_BlockageEnd == end)
                    {
                        car.m_BlockageStart = byte.MaxValue;
                        car.m_BlockageEnd = 0;
                        changed = true;
                    }
                }
            }
            if (!changed) return;
            EntityManager.SetComponentData(lane, car);
            if (!EntityManager.HasComponent<Created>(lane) &&
                !EntityManager.HasComponent<Updated>(lane) &&
                !EntityManager.HasComponent<PathfindUpdated>(lane))
                EntityManager.AddComponent<PathfindUpdated>(lane);
            Mod.Log.Info($"[TURN-LANE] Prepared center {lane} for controlled entry; native speed limit={car.m_SpeedLimit}, blockage={car.m_BlockageStart}..{car.m_BlockageEnd}.");
        }

        private struct JunctionGateResult
        {
            public int Closed;
            public bool StartMatched;
            public bool EndMatched;
        }

        internal bool IsDrivablePrototypeCenter(Entity lane)
        {
            var car = EntityManager.GetComponentData<Game.Net.CarLane>(lane);
            if ((car.m_Flags & CarLaneFlags.Twoway) == 0 ||
                !EntityManager.HasComponent<PrefabRef>(lane) ||
                !EntityManager.HasComponent<Owner>(lane)) return false;
            var prefabSystem = World.GetExistingSystemManaged<PrefabSystem>();
            if (prefabSystem == null) return false;
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
                built.Config?.ID == TurnLanePrefabSystem.DrivableRoadId;
            if (drivable) drivableRoadPrefabs[roadPrefabEntity] = true;
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
            if (built.Config?.ID == TurnLanePrefabSystem.DrivableRoadId) return false;
            var lanes = built.Config?.Lanes;
            if (lanes == null) return false;
            foreach (var lane in lanes)
            {
                string section = lane.SectionPrefabName ?? "";
                if (section == "TWTL NATIVE TWO-WAY UNSAFE TEST 3m" ||
                    section == "TWTL MEDIAN GRAPH TEST 3m" ||
                    section == "TWTL SEPARATOR GRAPH TEST 3m" ||
                    section == TurnLanePrefabSystem.PrefabName) return true;
            }
            return false;
        }
    }
}
