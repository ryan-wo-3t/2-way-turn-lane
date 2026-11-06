using System;
using System.Collections.Generic;
using System.Linq;

namespace Twtl.Core
{
    public enum ReservationState { Held, Occupied, Recovering }

    public sealed class ReservationRequest
    {
        public long Vehicle { get; }
        public long Corridor { get; }
        public int Revision { get; }
        public RouteIntent Intent { get; }
        public double VehicleLength { get; }
        public double Speed { get; }
        public double Deceleration { get; }
        public double Arrival { get; }
        public double ExpectedClearance { get; }
        public int Priority { get; }
        public ReservationRequest(long vehicle, long corridor, int revision, RouteIntent intent,
            double vehicleLength, double speed, double deceleration, double arrival, double expectedClearance, int priority = 0)
        {
            if (vehicle <= 0 || corridor <= 0 || revision < 0) throw new ArgumentOutOfRangeException(nameof(vehicle));
            Vehicle = vehicle; Corridor = corridor; Revision = revision; Intent = intent ?? throw new ArgumentNullException(nameof(intent));
            VehicleLength = Numbers.Positive(vehicleLength, nameof(vehicleLength));
            Speed = Numbers.Nonnegative(speed, nameof(speed));
            Deceleration = Numbers.Positive(deceleration, nameof(deceleration));
            Arrival = Numbers.Nonnegative(arrival, nameof(arrival));
            ExpectedClearance = Numbers.Positive(expectedClearance, nameof(expectedClearance));
            if (expectedClearance <= arrival) throw new ArgumentException("Clearance must follow arrival");
            Priority = priority;
        }
    }

    public sealed class Reservation
    {
        public ReservationRequest Request { get; }
        public Interval ProtectedSpace { get; }
        public ReservationState State { get; internal set; }
        internal Reservation(ReservationRequest request, Interval space) { Request = request; ProtectedSpace = space; }
    }

    public sealed class Admission
    {
        public long Vehicle { get; }
        public bool Granted { get; }
        public string Reason { get; }
        internal Admission(long vehicle, bool granted, string reason) { Vehicle = vehicle; Granted = granted; Reason = reason; }
    }

    /// <summary>
    /// Engine-independent conservative experiment. Call only on a serialized owner thread.
    /// Reserves swept physical space until clearance is confirmed, never ETA alone. Both
    /// directions may hold disjoint portions concurrently. This does not actuate game vehicles.
    /// </summary>
    public sealed class ReservationManager
    {
        private sealed class Corridor
        {
            public readonly int Revision;
            public readonly Interval Shared;
            public bool Open;
            public readonly List<Reservation> Reservations = new List<Reservation>();
            public Corridor(int revision, Interval shared) { Revision = revision; Shared = shared; }
        }
        private readonly Dictionary<long, Corridor> corridors = new Dictionary<long, Corridor>();
        private readonly Dictionary<long, Reservation> vehicles = new Dictionary<long, Reservation>();
        private readonly TwtlOptions options;
        private readonly Handedness handedness;
        private double lastTime;
        public ReservationManager(TwtlOptions options, Handedness handedness)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            TrafficRules.SharedTurn(handedness); this.handedness = handedness;
        }

        /// <summary>Call only after authoritative occupancy reconciliation. Never overwrite active holds.</summary>
        public void Register(long id, int revision, Interval shared, ValidationResult evidence, bool occupancyReconciled)
        {
            if (id <= 0 || revision < 0) throw new ArgumentOutOfRangeException(nameof(id));
            if (corridors.TryGetValue(id, out var old) && old.Reservations.Count != 0)
                throw new InvalidOperationException("Drain and confirm clearance before re-registering a corridor");
            corridors[id] = new Corridor(revision, shared) { Open = evidence.Status == ValidationStatus.Valid && occupancyReconciled };
        }

        private void Advance(double now)
        {
            Numbers.Nonnegative(now, nameof(now));
            if (now < lastTime) throw new ArgumentException("Simulation time moved backwards; reset after load");
            lastTime = now;
        }

        public IReadOnlyList<Admission> AdmitBatch(IEnumerable<ReservationRequest> requests, double now)
        {
            Advance(now);
            var batch = requests.ToArray();
            // Duplicate identities would make tie resolution depend on enumeration order.
            var duplicates = new HashSet<long>(batch.GroupBy(x => x.Vehicle).Where(g => g.Count() > 1).Select(g => g.Key));
            var result = new List<Admission>();
            foreach (var request in batch.OrderByDescending(x => x.Priority).ThenBy(x => x.Arrival).ThenBy(x => x.Vehicle))
            {
                string? rejection = null;
                if (duplicates.Contains(request.Vehicle)) rejection = "Duplicate vehicle request in batch";
                else if (vehicles.ContainsKey(request.Vehicle)) rejection = "Vehicle already has a reservation";
                else if (!corridors.TryGetValue(request.Corridor, out var road) || !road.Open || road.Revision != request.Revision)
                    rejection = "Corridor closed, unknown or geometry revision changed";
                else if (request.ExpectedClearance <= now) rejection = "Request already expired";
                else if (request.Speed > options.SpeedLimit || !RoutePolicy.Eligible(request.Intent, handedness, options, road.Shared))
                    rejection = "Unverified/ineligible turn, distance, exit or entry speed";
                else
                {
                    // Both ends include full body clearance. Directional stopping envelope also
                    // protects uncertainty at entry/exit. This is deliberately conservative.
                    double padding = request.VehicleLength / 2 + options.StandstillClearance;
                    double braking = TrafficRules.StoppingDistance(request.Speed, request.Deceleration, options.ReactionSeconds);
                    double min = Math.Min(request.Intent.Entry, request.Intent.Exit) - padding;
                    double max = Math.Max(request.Intent.Entry, request.Intent.Exit) + padding;
                    if (request.Intent.Direction == Direction.Forward) max += braking; else min -= braking;
                    var space = new Interval(min, max);
                    if (min < road.Shared.Min || max > road.Shared.Max)
                        rejection = "Insufficient room for body and braking clearance";
                    else if (road.Reservations.Any(x => x.ProtectedSpace.Overlaps(space)))
                        rejection = "Physical space conflicts with an existing reservation";
                    else
                    {
                        var reservation = new Reservation(request, space);
                        road.Reservations.Add(reservation); vehicles.Add(request.Vehicle, reservation);
                    }
                }
                result.Add(new Admission(request.Vehicle, rejection == null, rejection ?? "Granted pending authoritative entry"));
            }
            return result.AsReadOnly();
        }

        public bool ConfirmEntered(long vehicle)
        {
            if (!vehicles.TryGetValue(vehicle, out var reservation) || reservation.State != ReservationState.Held) return false;
            var road = corridors[reservation.Request.Corridor];
            if (!road.Open) return false;
            reservation.State = ReservationState.Occupied; return true;
        }

        /// <summary>A route change does not prove an occupied corridor is empty.</summary>
        public void Cancel(long vehicle)
        {
            if (!vehicles.TryGetValue(vehicle, out var reservation)) return;
            if (reservation.State == ReservationState.Held) Remove(reservation);
            else
            {
                reservation.State = ReservationState.Recovering;
                corridors[reservation.Request.Corridor].Open = false;
            }
        }

        public bool ConfirmCleared(long vehicle)
        {
            if (!vehicles.TryGetValue(vehicle, out var reservation)) return false;
            Remove(reservation); return true;
        }

        private void Remove(Reservation reservation)
        {
            corridors[reservation.Request.Corridor].Reservations.Remove(reservation);
            vehicles.Remove(reservation.Request.Vehicle);
        }

        public void Reconcile(double now, Func<long, bool> exists, Func<long, bool> routeStillValid)
        {
            Advance(now);
            foreach (var reservation in vehicles.Values.ToArray())
            {
                long id = reservation.Request.Vehicle;
                // exists=false must be authoritative despawn, including entity generation.
                if (!exists(id)) { Remove(reservation); continue; }
                if (reservation.State == ReservationState.Held && reservation.Request.ExpectedClearance <= now)
                { Remove(reservation); continue; }
                if (!routeStillValid(id)) Cancel(id);
                if (vehicles.ContainsKey(id) && reservation.State != ReservationState.Held && reservation.Request.ExpectedClearance <= now)
                {
                    reservation.State = ReservationState.Recovering;
                    corridors[reservation.Request.Corridor].Open = false;
                }
            }
        }

        public void Invalidate(long corridor)
        {
            if (!corridors.TryGetValue(corridor, out var road)) return;
            road.Open = false;
            foreach (var reservation in road.Reservations.ToArray()) Cancel(reservation.Request.Vehicle);
        }

        /// <summary>Only use after actual road deletion, not merely a configuration edit.</summary>
        public void RoadDeleted(long corridor)
        {
            if (!corridors.TryGetValue(corridor, out var road)) return;
            foreach (var reservation in road.Reservations.ToArray()) Remove(reservation);
            corridors.Remove(corridor);
        }

        public IReadOnlyList<Reservation> Snapshot(long corridor) => corridors.TryGetValue(corridor, out var road)
            ? Array.AsReadOnly(road.Reservations.ToArray()) : Array.Empty<Reservation>();
        // Clear on load/world change; all roads must be re-registered with reconciled occupancy.
        public void Reset() { corridors.Clear(); vehicles.Clear(); lastTime = 0; }
    }
}
