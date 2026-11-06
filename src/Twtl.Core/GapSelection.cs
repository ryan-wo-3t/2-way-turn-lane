using System;
using System.Collections.Generic;
using System.Linq;

namespace Twtl.Core
{
    /// <summary>Positions and speeds projected onto the receiving lane's travel axis (forward positive).</summary>
    public sealed class LaneVehicle
    {
        public long Id { get; }
        public double Position { get; }
        public double Speed { get; }
        public double Length { get; }
        public LaneVehicle(long id, double position, double speed, double length)
        {
            if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
            Id = id; Position = Numbers.Finite(position, nameof(position));
            Speed = Numbers.Nonnegative(speed, nameof(speed)); Length = Numbers.Positive(length, nameof(length));
        }
    }

    public sealed class MergePlan
    {
        public bool GapAvailable { get; }
        public long RearId { get; }
        public long FrontId { get; }
        public double SuggestedAcceleration { get; }
        // Always false: executing a lane change needs current native safety checks and a reservation.
        public bool MayInitiateLaneChange => false;
        internal MergePlan(bool gap, long rear, long front, double acceleration)
        { GapAvailable = gap; RearId = rear; FrontId = front; SuggestedAcceleration = acceleration; }
    }

    public static class GapSelection
    {
        /// <summary>
        /// Predicts constant-speed neighbours and bounded-acceleration ego trajectories. The
        /// analytic clearance minimum over the whole horizon prevents passing through a car
        /// merely because there is space at the endpoint. Input must cover the reachable area.
        /// Output is advice to an eventual adapter before vanilla collision speed calculations.
        /// </summary>
        public static MergePlan Select(LaneVehicle ego, IReadOnlyList<LaneVehicle> neighbours,
            double horizon, double maxAcceleration, double maxDeceleration, double speedCap,
            double minClearance, double headwaySeconds, bool corridorAheadReserved,
            MergePlan? previous = null, double hysteresis = 0.5)
        {
            Numbers.Positive(horizon, nameof(horizon)); Numbers.Positive(maxAcceleration, nameof(maxAcceleration));
            Numbers.Positive(maxDeceleration, nameof(maxDeceleration)); Numbers.Positive(speedCap, nameof(speedCap));
            Numbers.Positive(minClearance, nameof(minClearance)); Numbers.Positive(headwaySeconds, nameof(headwaySeconds));
            Numbers.Nonnegative(hysteresis, nameof(hysteresis));
            if (neighbours.Any(x => x.Id == ego.Id) || neighbours.Select(x => x.Id).Distinct().Count() != neighbours.Count)
                throw new ArgumentException("Neighbour identities must be unique and exclude ego");
            if (ego.Speed > speedCap || !corridorAheadReserved)
                return new MergePlan(false, 0, 0, -Math.Min(maxDeceleration, ego.Speed / horizon));
            var ordered = neighbours.OrderBy(x => x.Position + x.Speed * horizon).ThenBy(x => x.Id).ToArray();
            var plans = new List<(MergePlan Plan, double Cost)>();
            // Bounded candidate set controls acceleration, coasting and yielding. No direct speed mutation.
            var accelerations = new[] { -Math.Min(maxDeceleration, ego.Speed / horizon), 0,
                Math.Min(maxAcceleration, (speedCap - ego.Speed) / horizon) }.Distinct();
            foreach (double acceleration in accelerations)
            {
                double target = ego.Position + ego.Speed * horizon + 0.5 * acceleration * horizon * horizon;
                int slot = Array.FindIndex(ordered, x => x.Position + x.Speed * horizon >= target);
                if (slot < 0) slot = ordered.Length;
                var rear = slot > 0 ? ordered[slot - 1] : null;
                var front = slot < ordered.Length ? ordered[slot] : null;
                double endSpeed = ego.Speed + acceleration * horizon;
                bool safe = true;
                foreach (var other in ordered)
                {
                    bool ahead = other.Position + other.Speed * horizon >= target;
                    double body = (ego.Length + other.Length) / 2;
                    double clearance = minClearance + headwaySeconds * (ahead ? Math.Max(ego.Speed, endSpeed) : other.Speed);
                    // Signed centre separation is quadratic in time. Require no crossing at any point.
                    double c = ahead ? other.Position - ego.Position : ego.Position - other.Position;
                    double b = ahead ? other.Speed - ego.Speed : ego.Speed - other.Speed;
                    double a = ahead ? -0.5 * acceleration : 0.5 * acceleration;
                    double minimum = Math.Min(c, c + b * horizon + a * horizon * horizon);
                    if (a > 0)
                    {
                        double extremum = -b / (2 * a);
                        if (extremum > 0 && extremum < horizon)
                            minimum = Math.Min(minimum, c + b * extremum + a * extremum * extremum);
                    }
                    if (minimum < body + clearance) { safe = false; break; }
                }
                if (!safe) continue;
                var plan = new MergePlan(true, rear?.Id ?? 0, front?.Id ?? 0, acceleration);
                double cost = Math.Abs(acceleration);
                if (previous != null && previous.GapAvailable &&
                    (previous.RearId != plan.RearId || previous.FrontId != plan.FrontId)) cost += hysteresis;
                plans.Add((plan, cost));
            }
            return plans.OrderBy(x => x.Cost).ThenBy(x => x.Plan.SuggestedAcceleration).Select(x => x.Plan).FirstOrDefault()
                ?? new MergePlan(false, 0, 0, -Math.Min(maxDeceleration, ego.Speed / horizon));
        }
    }
}
