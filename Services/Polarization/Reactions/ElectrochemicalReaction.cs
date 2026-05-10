using System;

namespace CSaVe_Electrochemical_Data.Services.Polarization.Reactions
{
    public enum ReactionType
    {
        MetalOxidation,
        OxygenReduction,
        HydrogenEvolution
    }

    /// <summary>
    /// Base class storing all thermodynamic and kinetic data for a single electrochemical half-reaction.
    /// Provides derived thermodynamic quantities via computed properties and implements
    /// <see cref="IBvReaction"/> so that reaction objects are interchangeable through the interface.
    /// Universal physical constants are defined in <see cref="ElectrochemicalConstants"/>.
    /// Derive from this class (e.g. <see cref="HerReaction"/>, <see cref="OrrReaction"/>,
    /// <see cref="MetalOxidationReaction"/>) to add a new reaction without modifying
    /// <see cref="BvCurveFitter"/> (Open/Closed Principle).
    /// </summary>
    public abstract class ElectrochemicalReaction : IBvReaction
    {
        // ── Reaction-specific properties ──────────────────────────────────────────────────────────
        /// <summary>Descriptive name of the reaction (e.g., "HER").</summary>
        public ReactionType Name { get; }

        /// <summary>Standard reduction potential vs. SHE (V) at pH = 0.</summary>
        public double E0Vshe { get; }

        /// <summary>Number of electrons transferred in the half-reaction.</summary>
        public int Z { get; }

        // ── Environmental defaults (used when XML metadata is unavailable) ─────────────────────────
        /// <summary>Solution pH.</summary>
        public double pH { get; }

        /// <summary>Electrolyte temperature (°C).</summary>
        public double TemperatureCelsius { get; }

        // ── Derived quantities ────────────────────────────────────────────────────────────────────
        /// <summary>Electrolyte temperature (K).</summary>
        public double TemperatureKelvin => TemperatureCelsius + 273.15;

        /// <summary>
        /// Equilibrium potential vs. SHE (V) evaluated from the Nernst equation:
        /// E_eq = E0 − (R·T / z·F) · ln(10) · pH.
        /// Subclasses may override to add additional concentration correction terms.
        /// </summary>
        public virtual double EquilibriumPotentialVshe =>
            E0Vshe - (ElectrochemicalConstants.R * TemperatureKelvin / (Z * ElectrochemicalConstants.F)) * Math.Log(10.0) * pH;

        /// <summary>Thermal voltage V_T = R·T / (z·F) (V).</summary>
        public double ThermalVoltageV => ElectrochemicalConstants.R * TemperatureKelvin / (Z * ElectrochemicalConstants.F);

        // ── Fitted-parameter box bounds for LM optimisation ──────────────────────────────────────────
        /// <summary>Minimum physically meaningful exchange current density for LM box bounds (A/cm²).</summary>
        public virtual double I0MinAcm2 => 1.0e-30;

        /// <summary>Maximum exchange current density for LM box bounds (A/cm²).</summary>
        public virtual double I0MaxAcm2 => 1.0e-1;

        /// <summary>Minimum symmetry factor β for LM box bounds (dimensionless, 0 &lt; β &lt; 1).</summary>
        public virtual double BetaMin => 0.01;

        /// <summary>Maximum symmetry factor β for LM box bounds (dimensionless, 0 &lt; β &lt; 1).</summary>
        public virtual double BetaMax => 0.99;

        /// <summary>
        /// Minimum limiting current density for LM box bounds (A/cm²).
        /// Zero for reactions that do not have a mass-transport limiting current.
        /// Overridden by <see cref="OrrReaction"/>.
        /// </summary>
        public virtual double IlimMinAcm2 => 0.0;

        /// <summary>
        /// Maximum limiting current density for LM box bounds (A/cm²).
        /// Zero for reactions that do not have a mass-transport limiting current.
        /// Overridden by <see cref="OrrReaction"/>.
        /// </summary>
        public virtual double IlimMaxAcm2 => 0.0;

        // ── Constructor ───────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Initialises a new <see cref="ElectrochemicalReaction"/>.
        /// </summary>
        /// <param name="name">Descriptive name (e.g., "HER").</param>
        /// <param name="e0Vshe">Standard reduction potential vs. SHE (V) at pH = 0.</param>
        /// <param name="z">Number of electrons transferred.</param>
        /// <param name="pH">Solution pH (default 8.0).</param>
        /// <param name="temperatureCelsius">Temperature in °C (default 25.0).</param>
        public ElectrochemicalReaction(
            ReactionType name,
            double e0Vshe,
            int    z,
            double pH                = 8.0,
            double temperatureCelsius = 25.0)
        {
            Name               = name;
            E0Vshe             = e0Vshe;
            Z                  = z;
            this.pH            = pH;
            TemperatureCelsius = temperatureCelsius;
        }
    }
}
