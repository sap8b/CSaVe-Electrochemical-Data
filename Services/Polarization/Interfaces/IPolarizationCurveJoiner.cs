using System.Collections.Generic;

namespace CSaVe_Electrochemical_Data
{
    /// <summary>
    /// Joins separately recorded anodic and cathodic polarization curves into a single
    /// merged curve that is suitable for Butler-Volmer fitting.
    /// </summary>
    /// <remarks>
    /// Each branch is recorded as a separate potentiodynamic experiment.  The OCP can
    /// drift slightly between the two experiments, which would produce a gap or step
    /// artefact near Ecorr in the merged curve.  This interface specifies an OCP-alignment
    /// strategy:
    ///
    ///   1. Find OCP in each branch independently as the potential at minimum |I|.
    ///   2. Compute halfDiff = (V_ocp_anodic – V_ocp_cathodic) / 2.
    ///   3. Shift anodic potentials down by halfDiff and cathodic up by halfDiff so both
    ///      branches share the same aligned OCP.
    ///   4. Discard the short "wrong-side" segment from each branch that extends across
    ///      the aligned OCP:
    ///        – Anodic branch  -> keep only points where V &gt;= alignedOcp
    ///        – Cathodic branch -> keep only points where V &lt;  alignedOcp
    ///   5. Merge and sort by potential ascending.
    /// </remarks>
    public interface IPolarizationCurveJoiner
    {
        /// <summary>
        /// Join the forward-scan anodic and cathodic curves into a single merged curve.
        /// Both input lists must already have the return sweep removed (see
        /// <see cref="IMonotonicityFilter"/>).
        /// </summary>
        /// <param name="anodicPoints">Forward-scan anodic data points.</param>
        /// <param name="cathodicPoints">Forward-scan cathodic data points.</param>
        /// <returns>Merged and sorted polarization curve ready for BV fitting.</returns>
        IReadOnlyList<PolarizationPoint> Join(
            IReadOnlyList<PolarizationPoint> anodicPoints,
            IReadOnlyList<PolarizationPoint> cathodicPoints);
    }
}
