using System;

namespace Twtl.Core
{
    /// <summary>
    /// A conservative middle span for a disposable native graph experiment.
    /// It prevents one continuous traversal of a center edge while retaining
    /// short end approaches. It does not authorize or classify any vehicle.
    /// </summary>
    public static class TransitCut
    {
        public static Interval Plan(double laneLength, TwtlOptions options)
        {
            Numbers.Positive(laneLength, nameof(laneLength));
            if (options == null) throw new ArgumentNullException(nameof(options));
            double openEnd = Math.Min(laneLength * 0.35, options.IntendedDistance * 0.9);
            return new Interval(openEnd, laneLength - openEnd);
        }
    }
}
