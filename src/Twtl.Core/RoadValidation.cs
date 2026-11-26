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

    /// <summary>
    /// A complete, explicitly named set of runtime gates. An adapter must supply
    /// observed evidence for every gate; omitted or unknown evidence cannot
    /// activate a corridor. Yes for tram conflicts means either verified safe
    /// handling or a verified absence of tracks in this corridor.
    /// </summary>
    public sealed class RuntimeSafetyEvidence
    {
        public Evidence PhysicalTopology { get; }
        public Evidence BidirectionalPathGraph { get; }
        public Evidence TurnOnlyRouting { get; }
        public Evidence OpposingConflictDetection { get; }
        public Evidence EntryEnforcement { get; }
        public Evidence OccupancyReconciliation { get; }
        public Evidence SafeExitMerge { get; }
        public Evidence IntersectionTransitions { get; }
        public Evidence TramConflicts { get; }

        public RuntimeSafetyEvidence(Evidence physicalTopology, Evidence bidirectionalPathGraph,
            Evidence turnOnlyRouting, Evidence opposingConflictDetection, Evidence entryEnforcement,
            Evidence occupancyReconciliation, Evidence safeExitMerge, Evidence intersectionTransitions,
            Evidence tramConflicts)
        {
            Check(physicalTopology, nameof(physicalTopology));
            Check(bidirectionalPathGraph, nameof(bidirectionalPathGraph));
            Check(turnOnlyRouting, nameof(turnOnlyRouting));
            Check(opposingConflictDetection, nameof(opposingConflictDetection));
            Check(entryEnforcement, nameof(entryEnforcement));
            Check(occupancyReconciliation, nameof(occupancyReconciliation));
            Check(safeExitMerge, nameof(safeExitMerge));
            Check(intersectionTransitions, nameof(intersectionTransitions));
            Check(tramConflicts, nameof(tramConflicts));
            PhysicalTopology = physicalTopology;
            BidirectionalPathGraph = bidirectionalPathGraph;
            TurnOnlyRouting = turnOnlyRouting;
            OpposingConflictDetection = opposingConflictDetection;
            EntryEnforcement = entryEnforcement;
            OccupancyReconciliation = occupancyReconciliation;
            SafeExitMerge = safeExitMerge;
            IntersectionTransitions = intersectionTransitions;
            TramConflicts = tramConflicts;
        }

        public static RuntimeSafetyEvidence Unknown => new RuntimeSafetyEvidence(
            Evidence.Unknown, Evidence.Unknown, Evidence.Unknown, Evidence.Unknown, Evidence.Unknown,
            Evidence.Unknown, Evidence.Unknown, Evidence.Unknown, Evidence.Unknown);

        private static void Check(Evidence value, string name)
        {
            if (!Enum.IsDefined(typeof(Evidence), value)) throw new ArgumentOutOfRangeException(name);
        }
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

        /// <summary>Every named gate must be verified before runtime activation.</summary>
        public static ValidationResult Runtime(ValidationResult configuration, RuntimeSafetyEvidence evidence)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            if (evidence == null) throw new ArgumentNullException(nameof(evidence));
            if (configuration.Status == ValidationStatus.Invalid) return configuration;

            var checks = new[]
            {
                ("physical topology", evidence.PhysicalTopology),
                ("bidirectional path graph", evidence.BidirectionalPathGraph),
                ("turn-only routing", evidence.TurnOnlyRouting),
                ("opposing conflict detection", evidence.OpposingConflictDetection),
                ("entry enforcement", evidence.EntryEnforcement),
                ("occupancy reconciliation", evidence.OccupancyReconciliation),
                ("safe exit merge", evidence.SafeExitMerge),
                ("intersection transitions", evidence.IntersectionTransitions),
                ("tram conflict handling", evidence.TramConflicts)
            };
            var failed = checks.Where(check => check.Item2 == Evidence.No)
                .Select(check => check.Item1).ToArray();
            if (failed.Length != 0)
                return new ValidationResult(ValidationStatus.Invalid,
                    new[] { "Failed runtime checks: " + string.Join(", ", failed) + "." });
            if (configuration.Status == ValidationStatus.Warning) return configuration;
            var missing = checks.Where(check => check.Item2 == Evidence.Unknown)
                .Select(check => check.Item1).ToArray();
            if (missing.Length != 0)
                return new ValidationResult(ValidationStatus.Warning,
                    new[] { "Unverified runtime checks: " + string.Join(", ", missing) + "." });
            return new ValidationResult(ValidationStatus.Valid,
                new[] { "All required runtime checks verified for this corridor revision." });
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
