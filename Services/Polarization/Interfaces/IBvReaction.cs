using CSaVe_Electrochemical_Data.Services.Polarization.Reactions;

namespace CSaVe_Electrochemical_Data
{
    /// <summary>
    /// Defines the thermodynamic and kinetic data for a single electrochemical half-reaction
    /// used in the Butler-Volmer polarization curve model.
    /// Implement this interface to add a new reaction without modifying <see cref="BvCurveFitter"/>
    /// or any other existing class (Open/Closed Principle).
    /// </summary>
    public interface IBvReaction
    {
        /// <summary>Descriptive name of the reaction (e.g., "HER", "ORR", "Metal").</summary>
        ReactionType Name { get; }

        /// <summary>Standard reduction potential vs. SHE (V) at pH = 0.</summary>
        double E0Vshe { get; }

        /// <summary>Number of electrons transferred in the half-reaction.</summary>
        int Z { get; }

        /// <summary>Solution pH.</summary>
        double pH { get; }

        /// <summary>Electrolyte temperature (oC).</summary>
        double TemperatureCelsius { get; }

        /// <summary>Electrolyte temperature (K).</summary>
        double TemperatureKelvin { get; }

        /// <summary>
        /// Equilibrium potential vs. SHE (V) evaluated from the Nernst equation:
        /// E_eq = E0 − (R*T / z*F) * ln(10) * pH.
        /// </summary>
        double EquilibriumPotentialVshe { get; }

        /// <summary>Thermal voltage V_T = R*T / (z*F) (V).</summary>
        double ThermalVoltageV { get; }

        // ── Fitted-parameter box bounds ───────────────────────────────────────────────────────────
        /// <summary>Minimum physically meaningful exchange current density for LM box bounds (A/cm2).</summary>
        double I0MinAcm2 { get; }

        /// <summary>Maximum exchange current density for LM box bounds (A/cm2).</summary>
        double I0MaxAcm2 { get; }

        /// <summary>Minimum symmetry factor β for LM box bounds (dimensionless).</summary>
        double BetaMin { get; }

        /// <summary>Maximum symmetry factor β for LM box bounds (dimensionless).</summary>
        double BetaMax { get; }

        /// <summary>
        /// Minimum limiting current density for LM box bounds (A/cm2).
        /// Zero for reactions without a mass-transport limiting current.
        /// </summary>
        double IlimMinAcm2 { get; }

        /// <summary>
        /// Maximum limiting current density for LM box bounds (A/cm2).
        /// Zero for reactions without a mass-transport limiting current.
        /// </summary>
        double IlimMaxAcm2 { get; }
    }
}
