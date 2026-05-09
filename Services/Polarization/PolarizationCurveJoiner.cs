using System;
using System.Collections.Generic;
using System.Linq;

namespace CSaVe_Electrochemical_Data;

/// <summary>
/// Joins separately recorded anodic and cathodic polarization curves into a single
/// merged curve by aligning both branches to a common OCP and discarding the
/// "wrong-side" segments, then sorting the result by potential.
/// </summary>
public sealed class PolarizationCurveJoiner : IPolarizationCurveJoiner
{
    /// <summary>
    /// Join the forward-scan anodic and cathodic curves into a single merged curve.
    /// Both input lists must already have the return sweep removed (see
    /// <see cref="IMonotonicityFilter"/>).
    /// </summary>
    /// <param name="anodicPoints">Forward-scan anodic data points.</param>
    /// <param name="cathodicPoints">Forward-scan cathodic data points.</param>
    /// <returns>Merged and sorted polarization curve ready for BV fitting.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when either input list is empty.
    /// </exception>
    public IReadOnlyList<PolarizationPoint> Join(
        IReadOnlyList<PolarizationPoint> anodicPoints,
        IReadOnlyList<PolarizationPoint> cathodicPoints)
    {
        if (anodicPoints.Count == 0)
            throw new ArgumentException("Anodic branch contains no points.", nameof(anodicPoints));
        if (cathodicPoints.Count == 0)
            throw new ArgumentException("Cathodic branch contains no points.", nameof(cathodicPoints));

        // ── Step 1: find OCP in each branch as the potential at minimum |I| ───────────────
        double vOcpAnodic   = FindOcp(anodicPoints);
        double vOcpCathodic = FindOcp(cathodicPoints);

        // ── Step 2: compute the half-difference shift ──────────────────────────────────────
        // halfDiff > 0 when the anodic OCP is above the cathodic OCP (the common case).
        double halfDiff = (vOcpAnodic - vOcpCathodic) / 2.0;
        double vOcpMid  = vOcpAnodic - halfDiff; // == (vOcpAnodic + vOcpCathodic) / 2.0

        // ── Step 3: apply potential shift to each branch ──────────────────────────────────
        var shiftedAnodic   = ShiftPotentials(anodicPoints,   -halfDiff);
        var shiftedCathodic = ShiftPotentials(cathodicPoints, +halfDiff);

        // ── Step 4: trim both branches at the aligned OCP boundary ────────────────────────
        // Anodic branch is the oxidation side: keep V >= alignedOcp.
        // Cathodic branch is the reduction side: keep V < alignedOcp.
        var trimmedAnodic   = shiftedAnodic.Where(p => p.PotentialV >= vOcpMid).ToList();
        var trimmedCathodic = shiftedCathodic.Where(p => p.PotentialV < vOcpMid).ToList();

        // ── Step 5: merge and sort by potential ascending ─────────────────────────────────
        var merged = new List<PolarizationPoint>(trimmedAnodic.Count + trimmedCathodic.Count);
        merged.AddRange(trimmedAnodic);
        merged.AddRange(trimmedCathodic);
        merged.Sort((a, b) => a.PotentialV.CompareTo(b.PotentialV));

        return merged;
    }

    /// <summary>
    /// Find the open-circuit potential (OCP) in a branch as the potential at the
    /// point of minimum |I|.
    /// </summary>
    /// <param name="points">Branch data points.</param>
    /// <returns>Potential (V) at minimum |I|.</returns>
    private static double FindOcp(IReadOnlyList<PolarizationPoint> points)
    {
        int    ocpIndex  = 0;
        double minAbsI   = Math.Abs(points[0].CurrentA);

        for (int i = 1; i < points.Count; i++)
        {
            double absI = Math.Abs(points[i].CurrentA);
            if (absI < minAbsI)
            {
                minAbsI   = absI;
                ocpIndex  = i;
            }
        }

        return points[ocpIndex].PotentialV;
    }

    /// <summary>
    /// Return a new list of <see cref="PolarizationPoint"/> with each potential
    /// shifted by <paramref name="delta"/> (V).  Current values are unchanged.
    /// </summary>
    /// <param name="points">Source data points.</param>
    /// <param name="delta">Potential shift to apply (V); positive shifts up, negative shifts down.</param>
    /// <returns>New list with shifted potentials.</returns>
    private static List<PolarizationPoint> ShiftPotentials(IReadOnlyList<PolarizationPoint> points, double delta)
    {
        var result = new List<PolarizationPoint>(points.Count);
        foreach (var p in points)
            result.Add(new PolarizationPoint { PotentialV = p.PotentialV + delta, CurrentA = p.CurrentA });
        return result;
    }
}
