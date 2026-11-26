using System;
using System.Collections.Generic;

namespace Twtl.Core
{
    public enum TraceLaneKind { Unknown, Ordinary, SharedCenter, Junction, RoadsideConnection, Parking }
    public enum TraceResult { Incomplete, ThroughOrMerge, JunctionTurn, PossibleRoadsideTurn, TooLong }

    public readonly struct TraceStep
    {
        public TraceLaneKind Kind { get; }
        public double Metres { get; }
        public TurnSide Turn { get; }

        public TraceStep(TraceLaneKind kind, double metres, TurnSide turn = TurnSide.Unknown)
        {
            Kind = kind;
            Metres = Numbers.Nonnegative(metres, nameof(metres));
            Turn = turn;
        }
    }

    /// <summary>
    /// Classifies a visible native route around a shared center. A possible turn
    /// is evidence for testing only: lane sequence alone cannot prove that a
    /// destination is legal, that its geometry crosses the correct side, or
    /// that opposing traffic has yielded.
    /// </summary>
    public static class RouteTracePolicy
    {
        public static TraceResult Assess(IReadOnlyList<TraceStep> steps, int centerIndex,
            Handedness handedness, TwtlOptions options)
        {
            if (steps == null) throw new ArgumentNullException(nameof(steps));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (centerIndex < 0 || centerIndex >= steps.Count ||
                steps[centerIndex].Kind != TraceLaneKind.SharedCenter)
                return TraceResult.Incomplete;

            double distance = 0;
            int after = centerIndex;
            while (after < steps.Count && steps[after].Kind == TraceLaneKind.SharedCenter)
            {
                distance += steps[after].Metres;
                after++;
            }
            if (distance > options.IntendedDistance) return TraceResult.TooLong;
            if (after == steps.Count) return TraceResult.Incomplete;
            var exit = steps[after];
            if (exit.Kind == TraceLaneKind.Junction) return TraceResult.JunctionTurn;
            if (exit.Kind == TraceLaneKind.Ordinary) return TraceResult.ThroughOrMerge;
            if (exit.Kind != TraceLaneKind.RoadsideConnection &&
                exit.Kind != TraceLaneKind.Parking) return TraceResult.Incomplete;
            if (exit.Turn != TrafficRules.SharedTurn(handedness)) return TraceResult.Incomplete;
            return TraceResult.PossibleRoadsideTurn;
        }
    }
}
