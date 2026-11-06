using System;

namespace Twtl.Core
{
    public enum MovementPurpose { Unknown, Through, Overtake, RoadsideTurn, PropertyExitMerge }
    public enum RecoveryAction { FollowPlannedTurn, PlanExit, SeekGap, SlowOrStop }

    public sealed class RouteIntent
    {
        public MovementPurpose Purpose { get; }
        public TurnSide Turn { get; }
        public bool ConnectionVerified { get; }
        public bool ExitVerified { get; }
        public double Entry { get; }
        public double Exit { get; }
        public Direction Direction { get; }
        public RouteIntent(MovementPurpose purpose, TurnSide turn, bool connectionVerified, bool exitVerified,
            double entry, double exit, Direction direction)
        {
            Numbers.Check(direction);
            Purpose = purpose; Turn = turn; ConnectionVerified = connectionVerified; ExitVerified = exitVerified;
            Entry = Numbers.Finite(entry, nameof(entry)); Exit = Numbers.Finite(exit, nameof(exit)); Direction = direction;
        }
    }

    public static class RoutePolicy
    {
        public static bool Eligible(RouteIntent intent, Handedness handedness, TwtlOptions options, Interval shared)
        {
            if (intent.Purpose != MovementPurpose.RoadsideTurn && intent.Purpose != MovementPurpose.PropertyExitMerge)
                return false;
            double distance = (intent.Exit - intent.Entry) * (int)intent.Direction;
            return intent.ConnectionVerified && intent.ExitVerified && intent.Turn == TrafficRules.SharedTurn(handedness)
                && distance > 0 && distance <= options.IntendedDistance
                && shared.Contains(intent.Entry) && shared.Contains(intent.Exit);
        }

        public static RecoveryAction Recovery(double travelled, bool plannedTurnStillValid, bool safeExitAvailable,
            TwtlOptions options)
        {
            Numbers.Nonnegative(travelled, nameof(travelled));
            if (plannedTurnStillValid) return RecoveryAction.FollowPlannedTurn;
            if (travelled >= options.UrgentExitDistance && !safeExitAvailable) return RecoveryAction.SlowOrStop;
            if (travelled >= options.ExitPlanningDistance) return RecoveryAction.SeekGap;
            return RecoveryAction.PlanExit;
        }
    }
}
