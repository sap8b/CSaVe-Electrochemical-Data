using System.Collections.Generic;

namespace CSaVe_Electrochemical_Data;

/// <summary>
/// Fits a Butler-Volmer model to a merged polarization curve and returns the
/// fitted parameters along with per-point model current densities.
/// </summary>
public interface IBvCurveFitter
{
    /// <summary>
    /// Fit the BV model to <paramref name="currentDensityAcm2"/> vs
    /// <paramref name="potentialV"/>, using <paramref name="ecorrHintV"/> as a
    /// starting guess for Ecorr.
    /// </summary>
    /// <param name="potentialV">Potential values (V), sorted ascending.</param>
    /// <param name="currentDensityAcm2">Signed current density (A/cm²) at each potential.</param>
    /// <param name="ecorrHintV">Initial estimate for the corrosion potential (V).</param>
    /// <param name="temperatureCelsius">Electrolyte temperature (°C).</param>
    /// <returns>Fitted model parameters.</returns>
    BvModelParameters Fit(
        IReadOnlyList<double> potentialV,
        IReadOnlyList<double> currentDensityAcm2,
        double ecorrHintV,
        double temperatureCelsius);
}
