using System;

namespace Twtl.Core
{
    public readonly struct Position3
    {
        public double X { get; }
        public double Y { get; }
        public double Z { get; }
        public Position3(double x, double y, double z)
        { X = Numbers.Finite(x, nameof(x)); Y = Numbers.Finite(y, nameof(y)); Z = Numbers.Finite(z, nameof(z)); }

        public double Distance(Position3 other)
        {
            double x = X - other.X, y = Y - other.Y, z = Z - other.Z;
            return Math.Sqrt(x * x + y * y + z * z);
        }
    }

    /// <summary>Diagnostic geometry only. Matching curves do not establish traffic safety or route eligibility.</summary>
    public readonly struct CorridorCurve
    {
        public Position3 A { get; }
        public Position3 B { get; }
        public Position3 C { get; }
        public Position3 D { get; }
        public CorridorCurve(Position3 a, Position3 b, Position3 c, Position3 d)
        { A = a; B = b; C = c; D = d; }

        public bool IsReverseOf(CorridorCurve other, double toleranceMetres)
        {
            Numbers.Positive(toleranceMetres, nameof(toleranceMetres));
            // Reject near-degenerate endpoints: direction is ambiguous, including closed loops.
            if (A.Distance(D) <= 2 * toleranceMetres || other.A.Distance(other.D) <= 2 * toleranceMetres)
                return false;
            // A cubic's convex-hull property bounds the deviation over the entire curve.
            // Endpoint-only matching would incorrectly accept diverging curves.
            return A.Distance(other.D) <= toleranceMetres && B.Distance(other.C) <= toleranceMetres &&
                C.Distance(other.B) <= toleranceMetres && D.Distance(other.A) <= toleranceMetres;
        }
    }
}
