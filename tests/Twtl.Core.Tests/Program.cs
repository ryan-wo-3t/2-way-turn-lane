using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Twtl.Core;

internal static class Program
{
    private static int passed;
    private static int failed;
    private static readonly TwtlOptions Options = new TwtlOptions();
    private static readonly ValidationResult Verified = new ValidationResult(ValidationStatus.Valid, Array.Empty<string>());
    private static void Assert(bool condition, string message = "Assertion failed")
    { if (!condition) throw new Exception(message); }
    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new Exception("Expected " + typeof(T).Name);
    }
    private static void Test(string name, Action action)
    {
        try { action(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception error) { failed++; Console.WriteLine("FAIL " + name + ": " + error.Message); }
    }
    private static RoadStrip Travel(Direction direction) => new RoadStrip(StripKind.Travel, direction);
    private static List<RoadStrip> Road(int left, int right, Handedness handedness = Handedness.Right)
    {
        var direction = handedness == Handedness.Right ? Direction.Backward : Direction.Forward;
        return Enumerable.Repeat(Travel(direction), left).Concat(new[] { new RoadStrip(StripKind.SharedTurn) })
            .Concat(Enumerable.Repeat(Travel((Direction)(-(int)direction)), right)).ToList();
    }
    private static RouteIntent Intent(double entry, double exit, Direction direction = Direction.Forward,
        MovementPurpose purpose = MovementPurpose.RoadsideTurn, TurnSide side = TurnSide.Left,
        bool connection = true, bool validExit = true) => new RouteIntent(purpose, side, connection, validExit, entry, exit, direction);
    private static ReservationRequest Request(long vehicle, double entry = 50, double exit = 80,
        Direction direction = Direction.Forward, int priority = 0, double length = 4, double speed = 2,
        double arrival = 1, double clear = 20, int revision = 1, long corridor = 1) =>
        new ReservationRequest(vehicle, corridor, revision, Intent(entry, exit, direction), length, speed, 4, arrival, clear, priority);
    private static ReservationManager Manager()
    {
        var manager = new ReservationManager(Options, Handedness.Right);
        manager.Register(1, 1, new Interval(0, 500), Verified, true); return manager;
    }
    private static bool Grant(ReservationManager manager, ReservationRequest request, double now = 0) =>
        manager.AdmitBatch(new[] { request }, now)[0].Granted;
    private static MergePlan Gap(LaneVehicle ego, LaneVehicle[] others, bool reserved = true, MergePlan? previous = null) =>
        GapSelection.Select(ego, others, 3, 2, 3, 15, 2, 0.5, reserved, previous);

    public static int Main(string[] args)
    {
        if (args.Contains("--experiment")) { Experiment(); return 0; }
        Test("opposite curved corridor geometry is orientation independent", () =>
        {
            var a = new Position3(-50, 4, 10); var b = new Position3(-20, 6, 70);
            var c = new Position3(30, 8, 80); var d = new Position3(70, 12, 90);
            var curve = new CorridorCurve(a, b, c, d);
            Assert(curve.IsReverseOf(new CorridorCurve(d, c, b, a), 0.05));
            Assert(!curve.IsReverseOf(curve, 0.05));
        });
        Test("matching endpoints do not hide diverging or vertically separated curves", () =>
        {
            var a = new Position3(0, 0, 0); var b = new Position3(30, 0, 0);
            var c = new Position3(70, 0, 0); var d = new Position3(100, 0, 0);
            var curve = new CorridorCurve(a, b, c, d);
            Assert(!curve.IsReverseOf(new CorridorCurve(d, new Position3(70, 0, 5), b, a), 0.05));
            Assert(!curve.IsReverseOf(new CorridorCurve(new Position3(100, 3, 0), c, b, a), 0.05));
        });
        Test("reverse corridor comparison bounds numerical tolerance", () =>
        {
            var curve = new CorridorCurve(new Position3(0, 0, 0), new Position3(30, 0, 0),
                new Position3(70, 0, 0), new Position3(100, 0, 0));
            Assert(curve.IsReverseOf(new CorridorCurve(new Position3(100, 0.04, 0), curve.C, curve.B, curve.A), 0.05));
            Assert(!curve.IsReverseOf(new CorridorCurve(new Position3(100, 0.06, 0), curve.C, curve.B, curve.A), 0.05));
            Throws<ArgumentOutOfRangeException>(() => curve.IsReverseOf(curve, double.NaN));
            Throws<ArgumentOutOfRangeException>(() => curve.IsReverseOf(curve, 0));
        });
        Test("degenerate corridor direction and non-finite geometry rejected", () =>
        {
            Assert(!default(CorridorCurve).IsReverseOf(default, 0.05));
            Throws<ArgumentOutOfRangeException>(() => new Position3(0, double.PositiveInfinity, 0));
        });
        Test("touching and reversed interval boundaries", () =>
        {
            Assert(new Interval(1, 2).Overlaps(new Interval(2, 3)));
            Assert(!new Interval(1, 2).Overlaps(new Interval(2.01, 3)));
            Throws<ArgumentException>(() => new Interval(2, 1));
        });
        Test("non-finite safety input rejected", () =>
        {
            Throws<ArgumentOutOfRangeException>(() => new Interval(double.NaN, 5));
            Throws<ArgumentOutOfRangeException>(() => new TwtlOptions(speedLimit: double.PositiveInfinity));
            Throws<ArgumentOutOfRangeException>(() => Request(1, speed: -1));
        });
        Test("settings enforce planning order", () =>
        {
            Throws<ArgumentException>(() => new TwtlOptions(exitPlanningDistance: 60));
            Throws<ArgumentException>(() => new TwtlOptions(urgentExitDistance: 40));
            Assert(Options.IntendedDistance == 50 && Options.ExitPlanningDistance == 35 && Options.TransitionLength == 30);
        });
        Test("all symmetric and asymmetric layouts, no parity or maximum", () =>
        {
            foreach (var hand in new[] { Handedness.Right, Handedness.Left })
                foreach (int left in new[] { 1, 2, 3, 17, 100 })
                    foreach (int right in new[] { 1, 2, 3, 22 })
                        Assert(RoadValidation.Configuration(Road(left, right, hand), hand).Status == ValidationStatus.Valid);
        });
        Test("ordinary opposing travel lanes required", () =>
        {
            var road = Road(0, 2); road.Insert(0, new RoadStrip(StripKind.TramOnly, crossable: Evidence.Yes));
            Assert(RoadValidation.Configuration(road, Handedness.Right).Status == ValidationStatus.Invalid);
        });
        Test("wrong handedness and multiple corridors rejected", () =>
        {
            Assert(RoadValidation.Configuration(Road(1, 1), Handedness.Left).Status == ValidationStatus.Invalid);
            var road = Road(1, 1); road.Insert(1, new RoadStrip(StripKind.SharedTurn));
            Assert(RoadValidation.Configuration(road, Handedness.Right).Status == ValidationStatus.Invalid);
        });
        Test("physical barriers invalid, paint valid, outer sidewalks ignored", () =>
        {
            foreach (var kind in new[] { StripKind.RaisedMedian, StripKind.Barrier, StripKind.Sidewalk, StripKind.Parking, StripKind.Bicycle })
            {
                var road = Road(1, 1); road.Insert(1, new RoadStrip(kind));
                Assert(RoadValidation.Configuration(road, Handedness.Right).Status == ValidationStatus.Invalid);
            }
            var painted = Road(1, 1); painted.Insert(1, new RoadStrip(StripKind.Painted));
            painted.Insert(0, new RoadStrip(StripKind.Sidewalk));
            Assert(RoadValidation.Configuration(painted, Handedness.Right).Status == ValidationStatus.Valid);
        });
        Test("crossable trams warning, impassable trams invalid", () =>
        {
            foreach (var crossing in new[] { Evidence.Yes, Evidence.Unknown, Evidence.No })
            {
                var road = Road(2, 2); road.Insert(2, new RoadStrip(StripKind.TramOnly, crossable: crossing));
                Assert(RoadValidation.Configuration(road, Handedness.Right).Status ==
                    (crossing == Evidence.No ? ValidationStatus.Invalid : ValidationStatus.Warning));
            }
        });
        Test("runtime failure dominates unknown, warning blocks activation", () =>
        {
            Assert(RoadValidation.Runtime(Verified, Evidence.Unknown, Evidence.No).Status == ValidationStatus.Invalid);
            var manager = new ReservationManager(Options, Handedness.Right);
            manager.Register(1, 1, new Interval(0, 500), RoadValidation.Runtime(Verified), true);
            Assert(!Grant(manager, Request(1)));
        });
        Test("occupancy reconciliation required even after valid checks", () =>
        {
            var manager = new ReservationManager(Options, Handedness.Right);
            manager.Register(1, 1, new Interval(0, 500), Verified, false);
            Assert(!Grant(manager, Request(1)));
        });
        Test("through traffic, overtaking, unknown movements denied", () =>
        {
            foreach (var purpose in new[] { MovementPurpose.Through, MovementPurpose.Overtake, MovementPurpose.Unknown })
                Assert(!RoutePolicy.Eligible(Intent(10, 30, purpose: purpose), Handedness.Right, Options, new Interval(0, 100)));
            Assert(!RoutePolicy.Eligible(Intent(10, 30, connection: false), Handedness.Right, Options, new Interval(0, 100)));
            Assert(!RoutePolicy.Eligible(Intent(10, 30, validExit: false), Handedness.Right, Options, new Interval(0, 100)));
        });
        Test("LHT turns and property exit intent", () =>
        {
            Assert(RoutePolicy.Eligible(Intent(70, 40, Direction.Backward, MovementPurpose.PropertyExitMerge, TurnSide.Right),
                Handedness.Left, Options, new Interval(0, 100)));
            Assert(!RoutePolicy.Eligible(Intent(10, 30), Handedness.Left, Options, new Interval(0, 100)));
        });
        Test("distance and direction gate before admission", () =>
        {
            Assert(!Grant(Manager(), Request(1, 10, 61)));
            Assert(!Grant(Manager(), Request(1, 80, 50)));
            Assert(!Grant(Manager(), Request(1, speed: 20)));
        });
        Test("legitimate turn continues beyond intended distance", () =>
        {
            Assert(RoutePolicy.Recovery(90, true, false, Options) == RecoveryAction.FollowPlannedTurn);
            Assert(RoutePolicy.Recovery(50, false, false, Options) == RecoveryAction.SlowOrStop);
            Assert(RoutePolicy.Recovery(36, false, true, Options) == RecoveryAction.SeekGap);
        });
        Test("opposing conflicting reservations rejected", () =>
        {
            var manager = Manager(); Assert(Grant(manager, Request(1)));
            Assert(!Grant(manager, Request(2, 90, 60, Direction.Backward)));
        });
        Test("opposing disjoint sections simultaneously permitted", () =>
        {
            var manager = Manager();
            Assert(Grant(manager, Request(1))); Assert(Grant(manager, Request(2, 180, 150, Direction.Backward)));
        });
        Test("vehicle body and braking space protected", () =>
        {
            var manager = Manager(); Assert(Grant(manager, Request(1, 50, 70, length: 14, speed: 8)));
            Assert(!Grant(manager, Request(2, 98, 90, Direction.Backward)));
            Assert(!Grant(Manager(), Request(3, 1, 20)));
        });
        Test("deterministic priority independent of input order", () =>
        {
            var requests = new[] { Request(20), Request(10), Request(30, priority: 1) };
            foreach (var ordered in new[] { requests, requests.Reverse().ToArray() })
            {
                var decisions = Manager().AdmitBatch(ordered, 0);
                Assert(decisions.Single(x => x.Granted).Vehicle == 30);
            }
            Assert(Manager().AdmitBatch(requests.Take(2), 0).Single(x => x.Granted).Vehicle == 10);
        });
        Test("duplicate vehicle batch denied without arbitrary winner", () =>
            Assert(Manager().AdmitBatch(new[] { Request(1), Request(1, 150, 180) }, 0).All(x => !x.Granted)));
        Test("expired held requests cleaned, occupied reservations retained and closed", () =>
        {
            var manager = Manager(); Assert(Grant(manager, Request(1))); Assert(manager.ConfirmEntered(1, 0));
            manager.Reconcile(30, _ => true, _ => true);
            Assert(manager.Snapshot(1).Single().State == ReservationState.Recovering);
            Assert(!Grant(manager, Request(2, 180, 150, Direction.Backward, clear: 50), 30));
            var held = Manager(); Assert(Grant(held, Request(1))); held.Reconcile(30, _ => true, _ => true);
            Assert(held.Snapshot(1).Count == 0);
        });
        Test("route cancellation never clears occupied space", () =>
        {
            var manager = Manager(); Grant(manager, Request(1)); manager.ConfirmEntered(1, 0); manager.Cancel(1);
            Assert(manager.Snapshot(1).Count == 1); Assert(manager.ConfirmCleared(1));
            Assert(!Grant(manager, Request(2))); // explicit revalidation is required to reopen
            manager.Register(1, 2, new Interval(0, 500), Verified, true);
            Assert(!Grant(manager, Request(3))); Assert(Grant(manager, Request(4, revision: 2)));
        });
        Test("despawn, deletion, invalidation and reset cleanup", () =>
        {
            var manager = Manager(); Grant(manager, Request(1)); manager.ConfirmEntered(1, 0);
            manager.Reconcile(1, _ => false, _ => true); Assert(manager.Snapshot(1).Count == 0);
            Grant(manager, Request(2), 1); manager.Invalidate(1); Assert(manager.Snapshot(1).Count == 0);
            Assert(!Grant(manager, Request(3), 1)); manager.RoadDeleted(1);
            manager.Reset(); Assert(!Grant(manager, Request(4)));
        });
        Test("cannot overwrite occupied geometry; backwards time rejected", () =>
        {
            var manager = Manager(); Grant(manager, Request(1));
            Throws<InvalidOperationException>(() => manager.Register(1, 2, new Interval(0, 500), Verified, true));
            manager.Reconcile(2, _ => true, _ => true);
            Throws<ArgumentException>(() => manager.Reconcile(1, _ => true, _ => true));
        });
        Test("expired holds cannot enter even before the next reconciliation", () =>
        {
            var manager = Manager(); Grant(manager, Request(1));
            Assert(!manager.ConfirmEntered(1, 20)); Assert(manager.Snapshot(1).Count == 0);
        });
        Test("occupied hold invalidation retains space until actual clearance", () =>
        {
            var manager = Manager(); Grant(manager, Request(1)); manager.ConfirmEntered(1, 0);
            manager.Invalidate(1);
            Assert(manager.Snapshot(1).Single().State == ReservationState.Recovering);
            Assert(!Grant(manager, Request(2, 180, 150, Direction.Backward)));
            manager.RoadDeleted(1); Assert(manager.Snapshot(1).Count == 0);
        });
        Test("randomized accepted intervals never overlap", () =>
        {
            var manager = Manager(); var random = new Random(617);
            var requests = Enumerable.Range(1, 500).Select(i =>
            {
                double entry = random.Next(10, 480); bool forward = random.Next(2) == 0;
                return Request(i, entry, entry + (forward ? 10 : -10), forward ? Direction.Forward : Direction.Backward);
            }).ToArray();
            manager.AdmitBatch(requests, 0);
            var reservations = manager.Snapshot(1);
            Assert(reservations.Count > 1);
            for (int i = 0; i < reservations.Count; i++)
                for (int j = i + 1; j < reservations.Count; j++)
                    Assert(!reservations[i].ProtectedSpace.Overlaps(reservations[j].ProtectedSpace));
        });
        Test("short intersection fallback has no overlapping approaches", () =>
        {
            var shortPlan = Transitions.Plan(50, 30, 10, true, true, true, true);
            Assert(shortPlan.Shared == null && shortPlan.ForwardApproach == null && shortPlan.BackwardApproach == null);
            var normal = Transitions.Plan(150, 30, 10, true, true, false, true);
            Assert(normal.Shared?.Min == 30 && normal.Shared?.Max == 120);
            Assert(normal.BackwardApproach == null && normal.ForwardApproach?.Max == 150);
        });
        Test("merge acceleration selects reachable gap while respecting speed cap", () =>
        {
            var plan = Gap(new LaneVehicle(1, 100, 5, 4), new[] { new LaneVehicle(2, 80, 9, 4), new LaneVehicle(3, 160, 5, 4) });
            Assert(plan.GapAvailable && plan.SuggestedAcceleration > 0 && !plan.MayInitiateLaneChange);
        });
        Test("merge deceleration yields behind slower front vehicle", () =>
        {
            var plan = Gap(new LaneVehicle(1, 100, 10, 4), new[] { new LaneVehicle(2, 130, 0, 4) });
            Assert(plan.GapAvailable && plan.SuggestedAcceleration < 0);
        });
        Test("blocked merge slows, never forces lane change", () =>
        {
            var plan = Gap(new LaneVehicle(1, 100, 10, 4), new[] { new LaneVehicle(2, 101, 10, 4) });
            Assert(!plan.GapAvailable && plan.SuggestedAcceleration < 0 && !plan.MayInitiateLaneChange);
            Assert(!Gap(new LaneVehicle(1, 100, 10, 4), Array.Empty<LaneVehicle>(), false).GapAvailable);
        });
        Test("stable gap under unchanged observations and shuffled neighbours", () =>
        {
            var ego = new LaneVehicle(1, 100, 5, 4);
            var neighbours = new[] { new LaneVehicle(2, 60, 5, 4), new LaneVehicle(3, 150, 5, 4) };
            var previous = Gap(ego, neighbours);
            for (int i = 0; i < 30; i++)
            {
                var next = Gap(ego, neighbours.Reverse().ToArray(), previous: previous);
                Assert(next.GapAvailable && next.RearId == previous.RearId && next.FrontId == previous.FrontId);
                previous = next;
            }
        });
        Test("trajectory crossing rejected despite free endpoint", () =>
        {
            var plan = Gap(new LaneVehicle(1, 100, 10, 4), new[] { new LaneVehicle(2, 110, 0, 4) });
            Assert(!plan.GapAvailable);
        });
        Console.WriteLine($"RESULT {passed} passed, {failed} failed");
        return failed == 0 ? 0 : 1;
    }

    private static void Experiment()
    {
        Console.WriteLine("OFFLINE MODEL ONLY — no game lanes or vanilla braking are exercised.");
        var manager = Manager();
        var timer = Stopwatch.StartNew();
        var decisions = manager.AdmitBatch(new[] { Request(1), Request(2, 180, 150, Direction.Backward), Request(3, 90, 60, Direction.Backward) }, 0);
        timer.Stop();
        foreach (var decision in decisions) Console.WriteLine($"vehicle={decision.Vehicle} granted={decision.Granted} reason={decision.Reason}");
        foreach (var reservation in manager.Snapshot(1))
            Console.WriteLine($"vehicle={reservation.Request.Vehicle} direction={reservation.Request.Intent.Direction} space=[{reservation.ProtectedSpace.Min},{reservation.ProtectedSpace.Max}] state={reservation.State}");
        Console.WriteLine($"batchMs={timer.Elapsed.TotalMilliseconds:F3}; includes first-call JIT; not game overhead");
    }
}
