using System;

namespace Twtl.Core
{
    public enum Direction { Backward = -1, Forward = 1 }
    public enum Handedness { Right, Left }
    public enum TurnSide { Unknown, Left, Right, Straight }
    public enum Evidence { Unknown, Yes, No }
    public enum ValidationStatus { Valid, Warning, Invalid }

    public static class Numbers
    {
        public static double Finite(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(name);
            return value;
        }
        public static double Nonnegative(double value, string name)
        {
            if (Finite(value, name) < 0) throw new ArgumentOutOfRangeException(name);
            return value;
        }
        public static double Positive(double value, string name)
        {
            if (Finite(value, name) <= 0) throw new ArgumentOutOfRangeException(name);
            return value;
        }
        public static void Check(Direction direction)
        {
            if (direction != Direction.Forward && direction != Direction.Backward)
                throw new ArgumentOutOfRangeException(nameof(direction));
        }
    }

    /// <summary>Metres in a common segment coordinate, never logical lane curve parameters.</summary>
    public readonly struct Interval
    {
        public double Min { get; }
        public double Max { get; }
        public Interval(double min, double max)
        {
            Min = Numbers.Finite(min, nameof(min));
            Max = Numbers.Finite(max, nameof(max));
            if (min > max) throw new ArgumentException("Reversed interval");
        }
        // Touching bounds conflict; adapters must include numerical and geometric uncertainty in padding.
        public bool Overlaps(Interval other) => Min <= other.Max && other.Min <= Max;
        public bool Contains(double position) => Numbers.Finite(position, nameof(position)) >= Min && position <= Max;
    }

    public sealed class TwtlOptions
    {
        public double IntendedDistance { get; }
        public double ExitPlanningDistance { get; }
        public double UrgentExitDistance { get; }
        public double TransitionLength { get; }
        public double SpeedLimit { get; }
        public double StandstillClearance { get; }
        public double ReactionSeconds { get; }
        public TwtlOptions(double intendedDistance = 50, double exitPlanningDistance = 35,
            double urgentExitDistance = 50, double transitionLength = 30, double speedLimit = 8.33,
            double standstillClearance = 2, double reactionSeconds = 1.5)
        {
            IntendedDistance = Numbers.Positive(intendedDistance, nameof(intendedDistance));
            ExitPlanningDistance = Numbers.Nonnegative(exitPlanningDistance, nameof(exitPlanningDistance));
            UrgentExitDistance = Numbers.Positive(urgentExitDistance, nameof(urgentExitDistance));
            TransitionLength = Numbers.Positive(transitionLength, nameof(transitionLength));
            SpeedLimit = Numbers.Positive(speedLimit, nameof(speedLimit));
            StandstillClearance = Numbers.Positive(standstillClearance, nameof(standstillClearance));
            ReactionSeconds = Numbers.Positive(reactionSeconds, nameof(reactionSeconds));
            if (exitPlanningDistance > intendedDistance || urgentExitDistance < intendedDistance)
                throw new ArgumentException("Require planning <= intended <= urgent distance");
        }
    }

    public static class TrafficRules
    {
        public static TurnSide SharedTurn(Handedness handedness) => handedness == Handedness.Right
            ? TurnSide.Left : handedness == Handedness.Left ? TurnSide.Right
            : throw new ArgumentOutOfRangeException(nameof(handedness));

        public static double StoppingDistance(double speed, double deceleration, double reactionSeconds)
        {
            Numbers.Nonnegative(speed, nameof(speed));
            Numbers.Positive(deceleration, nameof(deceleration));
            Numbers.Nonnegative(reactionSeconds, nameof(reactionSeconds));
            return speed * reactionSeconds + speed * speed / (2 * deceleration);
        }
    }
}
