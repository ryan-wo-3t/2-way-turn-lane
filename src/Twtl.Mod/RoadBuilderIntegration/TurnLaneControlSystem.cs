using System;
using System.Collections.Generic;
using Game;
using Game.Common;
using Game.Net;
using Game.Simulation;
using Game.Vehicles;
using Unity.Collections;
using Unity.Entities;

namespace Twtl.Mod.RoadBuilderIntegration
{
    /// <summary>
    /// Serial admission for the one physical shared lane on each road edge.
    /// Runs after native route selection and before car movement. This uses the
    /// game's own speed command, so denied cars brake through normal movement.
    /// Only an explicit roadside left turn can receive an admission.
    /// </summary>
    public partial class TurnLaneControlSystem : GameSystemBase
    {
        private sealed class Claim
        {
            public Entity Vehicle;
            public bool Entered;
        }

        private EntityQuery cars;
        private Entity city;
        private TurnLanePathSystem closure = null!;
        private readonly Dictionary<Entity, bool> centerCache = new Dictionary<Entity, bool>();
        private readonly Dictionary<Entity, Claim> claims = new Dictionary<Entity, Claim>();
        private readonly Dictionary<Entity, int> occupancy = new Dictionary<Entity, int>();
        private readonly Dictionary<Entity, Entity> soleOccupants = new Dictionary<Entity, Entity>();
        private readonly List<Entity> expired = new List<Entity>();

        protected override void OnCreate()
        {
            base.OnCreate();
            closure = World.GetOrCreateSystemManaged<TurnLanePathSystem>();
            cars = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<Car>(),
                    ComponentType.ReadOnly<CarCurrentLane>(),
                    ComponentType.ReadWrite<CarNavigation>(),
                    ComponentType.ReadOnly<CarNavigationLane>() },
                None = new[] { ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Game.Tools.Temp>() }
            });
        }

        protected override void OnUpdate()
        {
            Entity currentCity = World.GetExistingSystemManaged<CitySystem>()?.City ?? Entity.Null;
            if (currentCity == Entity.Null) return;
            if (currentCity != city)
            {
                city = currentCity;
                centerCache.Clear();
                claims.Clear();
            }
            if (cars.IsEmptyIgnoreFilter) return;

            using (var vehicles = cars.ToEntityArray(Allocator.Temp))
            {
                occupancy.Clear();
                soleOccupants.Clear();
                for (int i = 0; i < vehicles.Length; i++)
                {
                    Entity vehicle = vehicles[i];
                    Entity lane = EntityManager.GetComponentData<CarCurrentLane>(vehicle).m_Lane;
                    if (!IsCenter(lane)) continue;
                    occupancy.TryGetValue(lane, out int count);
                    occupancy[lane] = count + 1;
                    if (count == 0) soleOccupants[lane] = vehicle;
                    else soleOccupants.Remove(lane);
                }

                expired.Clear();
                foreach (var pair in claims)
                {
                    Entity vehicle = pair.Value.Vehicle;
                    if (!EntityManager.Exists(vehicle) ||
                        !EntityManager.HasComponent<CarCurrentLane>(vehicle) ||
                        !EntityManager.HasBuffer<CarNavigationLane>(vehicle))
                    {
                        expired.Add(pair.Key);
                        continue;
                    }
                    var current = EntityManager.GetComponentData<CarCurrentLane>(vehicle);
                    if (current.m_Lane == pair.Key)
                    {
                        pair.Value.Entered = true;
                        continue;
                    }
                    if (pair.Value.Entered || !PlansCenter(vehicle, pair.Key))
                        expired.Add(pair.Key);
                }
                foreach (Entity lane in expired) claims.Remove(lane);
                // After loading a city, the transient claim map is empty while
                // cars already in the physical lane still exist. Reconcile
                // from actual occupancy before accepting a new approach.
                foreach (var pair in soleOccupants)
                    claims[pair.Key] = new Claim { Vehicle = pair.Value, Entered = true };
                foreach (var pair in occupancy)
                    if (pair.Value > 1) claims.Remove(pair.Key);

                for (int i = 0; i < vehicles.Length; i++)
                {
                    Entity vehicle = vehicles[i];
                    var current = EntityManager.GetComponentData<CarCurrentLane>(vehicle);
                    if (IsCenter(current.m_Lane)) continue;
                    var navigation = EntityManager.GetBuffer<CarNavigationLane>(vehicle, true);
                    Entity center = IsCenter(current.m_ChangeLane) ? current.m_ChangeLane : Entity.Null;
                    int centerIndex = -1;
                    for (int j = 0; j < navigation.Length; j++)
                    {
                        if (!IsCenter(navigation[j].m_Lane)) continue;
                        center = navigation[j].m_Lane;
                        centerIndex = j;
                        break;
                    }
                    if (center == Entity.Null ||
                        (centerIndex > 0 && current.m_ChangeLane != center)) continue;

                    bool admitted = claims.TryGetValue(center, out Claim claim) && claim.Vehicle == vehicle;
                    if (!admitted && !claims.ContainsKey(center) &&
                        !occupancy.ContainsKey(center) &&
                        IsRoadsideLeftTurn(navigation, centerIndex, center))
                    {
                        claims[center] = new Claim { Vehicle = vehicle };
                        admitted = true;
                    }

                    var speed = EntityManager.GetComponentData<CarNavigation>(vehicle);
                    // The native move system applies acceleration and braking
                    // limits to this command. Never force a teleport or lane swap.
                    speed.m_MaxSpeed = admitted ? Math.Min(speed.m_MaxSpeed, 5f) : 0f;
                    EntityManager.SetComponentData(vehicle, speed);
                }
            }
        }

        private bool IsCenter(Entity lane)
        {
            if (lane == Entity.Null || !EntityManager.Exists(lane) ||
                !EntityManager.HasComponent<EdgeLane>(lane) ||
                !EntityManager.HasComponent<Game.Net.CarLane>(lane)) return false;
            if (!centerCache.TryGetValue(lane, out bool result))
            {
                result = closure.IsDrivablePrototypeCenter(lane);
                // A newly generated lane may acquire its owner/prefab later.
                // Only positive matches are stable enough to cache.
                if (result) centerCache[lane] = true;
            }
            return result;
        }

        private bool PlansCenter(Entity vehicle, Entity center)
        {
            var current = EntityManager.GetComponentData<CarCurrentLane>(vehicle);
            if (current.m_ChangeLane == center) return true;
            var navigation = EntityManager.GetBuffer<CarNavigationLane>(vehicle, true);
            for (int i = 0; i < navigation.Length; i++)
                if (navigation[i].m_Lane == center) return true;
            return false;
        }

        private bool IsRoadsideLeftTurn(DynamicBuffer<CarNavigationLane> navigation,
            int centerIndex, Entity center)
        {
            if (centerIndex < 0 || !EntityManager.HasComponent<Curve>(center)) return false;
            double length = EntityManager.GetComponentData<Curve>(center).m_Length;
            double travelled = 0;
            int direction = 0;
            for (int i = centerIndex; i < navigation.Length; i++)
            {
                var step = navigation[i];
                if (step.m_Lane != center)
                {
                    if (travelled <= 0 || travelled > 50 ||
                        !EntityManager.Exists(step.m_Lane) ||
                        !EntityManager.HasComponent<ConnectionLane>(step.m_Lane) ||
                        !EntityManager.HasComponent<Owner>(step.m_Lane) ||
                        (step.m_Flags & Game.Vehicles.CarLaneFlags.TurnLeft) == 0) return false;
                    var connection = EntityManager.GetComponentData<ConnectionLane>(step.m_Lane);
                    Entity destination = EntityManager.GetComponentData<Owner>(step.m_Lane).m_Owner;
                    return (connection.m_Flags & ConnectionLaneFlags.Road) != 0 &&
                        EntityManager.Exists(destination) &&
                        EntityManager.HasComponent<Game.Buildings.Building>(destination);
                }
                double delta = step.m_CurvePosition.y - step.m_CurvePosition.x;
                if (Math.Abs(delta) < 0.0001) return false;
                int stepDirection = delta > 0 ? 1 : -1;
                if (direction != 0 && stepDirection != direction) return false;
                direction = stepDirection;
                travelled += Math.Abs(delta) * length;
            }
            return false;
        }
    }
}
