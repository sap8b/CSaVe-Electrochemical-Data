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
        /// </summary>
        public double EquilibriumPotentialVshe =>
            E0Vshe - (ElectrochemicalConstants.R * TemperatureKelvin / (Z * ElectrochemicalConstants.F)) * Math.Log(10.0) * pH;

        /// <summary>Thermal voltage V_T = R·T / (z·F) (V).</summary>
        public double ThermalVoltageV => ElectrochemicalConstants.R * TemperatureKelvin / (Z * ElectrochemicalConstants.F);

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
