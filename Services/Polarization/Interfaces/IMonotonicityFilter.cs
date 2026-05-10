using System.Collections.Generic;

namespace CSaVe_Electrochemical_Data
{
    /// <summary>
    /// Enforces monotonicity of the potential sweep so that only the forward scan
    /// (up to and including the apex/nadir) is retained.
    /// </summary>
    /// <remarks>
    /// Electrochemical potentiodynamic scans record the return sweep as well.
    /// The return sweep introduces hysteresis and must be discarded before fitting.
    ///
    /// Anodic sweep  (forward = increasing V): keep up to the maximum potential,
    ///               then discard all points where V starts decreasing back.
    /// Cathodic sweep (forward = decreasing V): keep up to the minimum potential,
    ///               then discard points where V starts increasing back.
    /// A 5 mV noise tolerance is applied so that small instrument jitter at the apex
    /// does not prematurely terminate the forward scan.
    /// </remarks>
    public interface IMonotonicityFilter
    {
        /// <summary>
        /// Remove non-monotonic (return-sweep) points from <paramref name="points"/>.
        /// </summary>
        /// <param name="points">Raw data in file order.</param>
        /// <param name="isAnodic">
        /// <c>true</c> for an anodic sweep (potential increases to apex);
        /// <c>false</c> for a cathodic sweep (potential decreases to nadir).
        /// </param>
        /// <returns>Forward-scan-only points, in original order.</returns>
        IReadOnlyList<PolarizationPoint> Filter(IReadOnlyList<PolarizationPoint> points, bool isAnodic);
    }
}
