using System;

namespace CSaVe_Electrochemical_Data;

/// <summary>
/// Holds the 11-parameter Butler-Volmer model for polarization curve fitting.
/// Provides methods to evaluate the total and component current densities at a given potential.
/// </summary>
public sealed class BvModelParameters
{
    private static readonly double Ln10 = Math.Log(10.0);

    // ── Anodic dissolution branch ─────────────────────────────────────────────────────────────
    /// <summary>Anodic exchange current density I₀ₐ (A/cm²).</summary>
    public double I0Anodic { get; init; }

    /// <summary>Anodic Tafel slope βₐ (V/decade); governs the exponential rise of dissolution current.</summary>
    public double BetaAnodic { get; init; }

    // ── Cathodic activation branch (ORR Tafel) ────────────────────────────────────────────────
    /// <summary>Cathodic exchange current density I₀꜀ (A/cm²).</summary>
    public double I0Cathodic { get; init; }

    /// <summary>Cathodic Tafel slope β꜀ (V/decade); governs the exponential cathodic activation current.</summary>
    public double BetaCathodic { get; init; }

    // ── Corrosion potential ────────────────────────────────────────────────────────────────────
    /// <summary>Corrosion potential Ecorr (V vs. reference).</summary>
    public double Ecorr { get; init; }

    // ── ORR sigmoidal limiting current ────────────────────────────────────────────────────────
    /// <summary>ORR mass-transport limiting current density iₗᵢₘ,ₒᵣᵣ (A/cm²); always positive.</summary>
    public double IlimOrr { get; init; }

    /// <summary>Midpoint potential of the ORR sigmoidal transition (V vs. reference).</summary>
    public double EorrTransition { get; init; }

    /// <summary>Width parameter of the ORR sigmoid (V); smaller values give a sharper transition.</summary>
    public double WorrV { get; init; }

    // ── HER activation branch ─────────────────────────────────────────────────────────────────
    /// <summary>HER exchange current density I₀,ₕₑᵣ (A/cm²).</summary>
    public double I0Her { get; init; }

    /// <summary>HER Tafel slope βₕₑᵣ (V/decade).</summary>
    public double BetaHer { get; init; }

    /// <summary>HER onset potential Eₕₑᵣ (V vs. reference); the potential at which HER becomes significant.</summary>
    public double EherOnset { get; init; }

    // ── Exponential argument clip limits ─────────────────────────────────────────────────────
    // Clipping to [-50, 50] prevents overflow in exp() while retaining all physically meaningful values.
    private const double ExpClipMin = -50.0;
    private const double ExpClipMax =  50.0;

    /// <summary>
    /// Evaluates the full Butler-Volmer model at a single electrode potential and returns the
    /// net signed current density (A/cm²).
    /// </summary>
    /// <param name="potentialV">Electrode potential (V vs. reference).</param>
    /// <returns>Net signed current density (A/cm²); positive = net anodic.</returns>
    public double ComputeCurrentDensity(double potentialV)
    {
        double eta = potentialV - Ecorr;

        double iAnodic   = I0Anodic   * Math.Exp(Math.Clamp( eta * Ln10 / BetaAnodic,  ExpClipMin, ExpClipMax));
        double iCathodic = I0Cathodic * Math.Exp(Math.Clamp(-eta * Ln10 / BetaCathodic, ExpClipMin, ExpClipMax));
        double iOrr      = -IlimOrr   / (1.0 + Math.Exp(Math.Clamp((potentialV - EorrTransition) / WorrV, -50.0, 50.0)));
        double iHer      = -I0Her     * Math.Exp(Math.Clamp(-(potentialV - EherOnset) * Ln10 / BetaHer, ExpClipMin, ExpClipMax));

        return iAnodic - iCathodic + iOrr + iHer;
    }

    /// <summary>
    /// Evaluates only the anodic dissolution component at a single electrode potential.
    /// </summary>
    /// <param name="potentialV">Electrode potential (V vs. reference).</param>
    /// <returns>Anodic current density (A/cm²); always non-negative.</returns>
    public double ComputeAnodicComponent(double potentialV)
    {
        double eta = potentialV - Ecorr;
        return I0Anodic * Math.Exp(Math.Clamp(eta * Ln10 / BetaAnodic, ExpClipMin, ExpClipMax));
    }

    /// <summary>
    /// Evaluates only the ORR sigmoidal limiting-current component at a single electrode potential.
    /// </summary>
    /// <param name="potentialV">Electrode potential (V vs. reference).</param>
    /// <returns>ORR current density (A/cm²); always non-positive.</returns>
    public double ComputeOrrComponent(double potentialV)
    {
        return -IlimOrr / (1.0 + Math.Exp(Math.Clamp((potentialV - EorrTransition) / WorrV, -50.0, 50.0)));
    }

    /// <summary>
    /// Evaluates only the HER activation component at a single electrode potential.
    /// </summary>
    /// <param name="potentialV">Electrode potential (V vs. reference).</param>
    /// <returns>HER current density (A/cm²); always non-positive.</returns>
    public double ComputeHerComponent(double potentialV)
    {
        return -I0Her * Math.Exp(Math.Clamp(-(potentialV - EherOnset) * Ln10 / BetaHer, ExpClipMin, ExpClipMax));
    }
}
