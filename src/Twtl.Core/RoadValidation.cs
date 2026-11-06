using System;
using System.Collections.Generic;
using System.Linq;

namespace Twtl.Core
{
    public enum StripKind { Travel, SharedTurn, TramOnly, Painted, RaisedMedian, Barrier, Sidewalk, Parking, Bicycle }

    public sealed class RoadStrip
    {
        public StripKind Kind { get; }
        public Direction Direction { get; }
        public Evidence Crossable { get; }
        public double Width { get; }
        public RoadStrip(StripKind kind, Direction direction = Direction.Forward,
            Evidence crossable = Evidence.Unknown, double width = 3)
        {
            if (!Enum.IsDefined(typeof(StripKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            Numbers.Check(direction);
            Kind = kind; Direction = direction; Crossable = crossable;
            Width = Numbers.Positive(width, nameof(width));
        }
    }

    public sealed class ValidationResult
    {
        public ValidationStatus Status { get; }
        public IReadOnlyList<string> Reasons { get; }
        public ValidationResult(ValidationStatus status, IEnumerable<string> reasons)
        { Status = status; Reasons = Array.AsReadOnly(reasons.ToArray()); }
    }

    public static class RoadValidation
    {
        /// <summary>Strips ordered left to right looking along the segment's forward tangent.</summary>
        public static ValidationResult Configuration(IReadOnlyList<RoadStrip> strips, Handedness handedness)
        {
            if (strips == null) throw new ArgumentNullException(nameof(strips));
            TrafficRules.SharedTurn(handedness);
            var errors = new List<string>();
            var warnings = new List<string>();
            var shared = Enumerable.Range(0, strips.Count).Where(i => strips[i].Kind == StripKind.SharedTurn).ToArray();
            if (shared.Length != 1)
                return new ValidationResult(ValidationStatus.Invalid, new[] { "Exactly one shared physical corridor is required." });
            int center = shared[0];
            Direction left = handedness == Handedness.Right ? Direction.Backward : Direction.Forward;
            var leftTravel = Enumerable.Range(0, center).Where(i => strips[i].Kind == StripKind.Travel).ToArray();
            var rightTravel = Enumerable.Range(center + 1, strips.Count - center - 1)
                .Where(i => strips[i].Kind == StripKind.Travel).ToArray();
            if (leftTravel.Length == 0 || rightTravel.Length == 0)
                errors.Add("Ordinary vehicle travel lanes are required on both sides of the corridor.");
            if (leftTravel.Any(i => strips[i].Direction != left) || rightTravel.Any(i => strips[i].Direction == left))
                errors.Add("The corridor must separate opposing traffic in the city's traffic handedness.");
            // Only strips between the nearest ordinary lanes affect direct access. Outer sidewalks do not.
            if (leftTravel.Length > 0 && rightTravel.Length > 0)
            {
                for (int i = leftTravel.Last() + 1; i < rightTravel.First(); i++)
                {
                    if (i == center) continue;
                    var strip = strips[i];
                    if (strip.Kind == StripKind.RaisedMedian || strip.Kind == StripKind.Barrier ||
                        strip.Kind == StripKind.Sidewalk || strip.Kind == StripKind.Parking || strip.Kind == StripKind.Bicycle ||
                        (strip.Kind == StripKind.TramOnly && strip.Crossable == Evidence.No))
                        errors.Add($"Strip {i} blocks legal physical access to the shared corridor.");
                    else if (strip.Kind == StripKind.TramOnly)
                        warnings.Add($"Strip {i}: verify crossing topology and tram conflict handling in the game.");
                }
            }
            return errors.Count > 0 ? new ValidationResult(ValidationStatus.Invalid, errors)
                : warnings.Count > 0 ? new ValidationResult(ValidationStatus.Warning, warnings)
                : new ValidationResult(ValidationStatus.Valid, new[] { "Cross-section is physically eligible; runtime verification is separate." });
        }

        /// <summary>Unknown evidence never enables admissions. No traffic demand is required.</summary>
        public static ValidationResult Runtime(ValidationResult configuration, params Evidence[] requiredChecks)
        {
            if (configuration.Status == ValidationStatus.Invalid) return configuration;
            if (requiredChecks.Any(x => x == Evidence.No))
                return new ValidationResult(ValidationStatus.Invalid, new[] { "A required runtime safety or connection check failed." });
            if (requiredChecks.Length == 0 || requiredChecks.Any(x => x != Evidence.Yes))
                return new ValidationResult(ValidationStatus.Warning, new[] { "Runtime topology, routing, braking or occupancy evidence is incomplete." });
            return new ValidationResult(ValidationStatus.Valid, new[] { "All supplied runtime checks verified; adapter owns evidence completeness." });
        }
    }

    public sealed class TransitionPlan
    {
        public Interval? Shared { get; }
        public Interval? BackwardApproach { get; }
        public Interval? ForwardApproach { get; }
        public string Reason { get; }
        internal TransitionPlan(Interval? shared, Interval? backward, Interval? forward, string reason)
        { Shared = shared; BackwardApproach = backward; ForwardApproach = forward; Reason = reason; }
    }

    public static class Transitions
    {
        public static TransitionPlan Plan(double length, double transitionLength, double minimumSharedLength,
            bool startIntersection, bool endIntersection, bool startTurnAllowed, bool endTurnAllowed)
        {
            Numbers.Positive(length, nameof(length));
            Numbers.Positive(transitionLength, nameof(transitionLength));
            Numbers.Positive(minimumSharedLength, nameof(minimumSharedLength));
            double start = startIntersection ? transitionLength : 0;
            double end = endIntersection ? length - transitionLength : length;
            if (end - start < minimumSharedLength)
                return new TransitionPlan(null, null, null, "Insufficient room: keep the research corridor closed; no lane connections generated.");
            return new TransitionPlan(new Interval(start, end),
                startIntersection && startTurnAllowed ? new Interval(0, start) : (Interval?)null,
                endIntersection && endTurnAllowed ? new Interval(end, length) : (Interval?)null,
                "Approach intervals are planning data; geometry generation is not implemented.");
        }
    }
}
