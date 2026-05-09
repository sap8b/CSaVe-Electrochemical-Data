using System;
using System.Collections.Generic;

namespace CSaVe_Electrochemical_Data;

/// <summary>
/// Enforces monotonicity of the potential sweep so that only the forward scan
/// (up to and including the apex/nadir) is retained, with a 5 mV noise tolerance
/// to avoid prematurely terminating the scan due to instrument jitter at the apex.
/// </summary>
public sealed class MonotonicityFilter : IMonotonicityFilter
{
    // Noise tolerance at the apex (V): minor deviations within this band are accepted
    // as instrument jitter rather than as the start of the return sweep.
    private const double ApexNoiseTolerance = 0.005; // 5 mV

    /// <summary>
    /// Remove non-monotonic (return-sweep) points from <paramref name="points"/>.
    /// </summary>
    /// <param name="points">Raw data in file order.</param>
    /// <param name="isAnodic">
    /// <c>true</c> for an anodic sweep (potential increases to apex);
    /// <c>false</c> for a cathodic sweep (potential decreases to nadir).
    /// </param>
    /// <returns>Forward-scan-only points, in original order.</returns>
    public IReadOnlyList<PolarizationPoint> Filter(IReadOnlyList<PolarizationPoint> points, bool isAnodic)
    {
        if (points.Count == 0)
            return points;

        return isAnodic ? FilterAnodic(points) : FilterCathodic(points);
    }

    /// <summary>
    /// Filter an anodic (increasing potential) sweep: keep points up to and including
    /// the apex, defined as the potential maximum.  Stop as soon as V drops more than
    /// <see cref="ApexNoiseTolerance"/> below the running maximum.
    /// </summary>
    private static IReadOnlyList<PolarizationPoint> FilterAnodic(IReadOnlyList<PolarizationPoint> points)
    {
        int    apexIndex  = 0;
        double runningMax = points[0].PotentialV;

        for (int i = 1; i < points.Count; i++)
        {
            double v = points[i].PotentialV;

            if (v >= runningMax)
            {
                runningMax = v;
                apexIndex  = i;
            }
            else if (v < runningMax - ApexNoiseTolerance)
            {
                // V has fallen clearly below the running maximum — the return sweep has started.
                break;
            }
            // else: V is within the noise band below the running maximum — keep scanning forward.
        }

        return Slice(points, 0, apexIndex + 1);
    }

    /// <summary>
    /// Filter a cathodic (decreasing potential) sweep: keep points up to and including
    /// the nadir, defined as the potential minimum.  Stop as soon as V rises more than
    /// <see cref="ApexNoiseTolerance"/> above the running minimum.
    /// </summary>
    private static IReadOnlyList<PolarizationPoint> FilterCathodic(IReadOnlyList<PolarizationPoint> points)
    {
        int    apexIndex  = 0;
        double runningMin = points[0].PotentialV;

        for (int i = 1; i < points.Count; i++)
        {
            double v = points[i].PotentialV;

            if (v <= runningMin)
            {
                runningMin = v;
                apexIndex  = i;
            }
            else if (v > runningMin + ApexNoiseTolerance)
            {
                // V has risen clearly above the running minimum — the return sweep has started.
                break;
            }
            // else: V is within the noise band above the running minimum — keep scanning forward.
        }

        return Slice(points, 0, apexIndex + 1);
    }

    /// <summary>
    /// Return a sub-list of <paramref name="points"/> from index <paramref name="start"/>
    /// (inclusive) to <paramref name="end"/> (exclusive) without allocating an extra copy
    /// when the entire list is selected.
    /// </summary>
    private static IReadOnlyList<PolarizationPoint> Slice(IReadOnlyList<PolarizationPoint> points, int start, int end)
    {
        if (start == 0 && end == points.Count)
            return points;

        var result = new List<PolarizationPoint>(end - start);
        for (int i = start; i < end; i++)
            result.Add(points[i]);
        return result;
    }
}
