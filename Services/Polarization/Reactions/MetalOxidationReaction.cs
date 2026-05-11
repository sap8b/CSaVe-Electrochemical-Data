using CSaVe_Electrochemical_Data.Services.Polarization.Reactions;

using System;

namespace CSaVe_Electrochemical_Data
{
    /// <summary>
    /// Represents the metal oxidation (dissolution) half-reaction (e.g. Fe → Fe2- + 2e-).
    /// Uses an textbook −0.44 V for Fe/Fe2- standard potential of E₀ = −0.44 V vs. SHE.
    /// Electrons transferred: z = 2.
    /// Includes a Nernst concentration correction for the dissolved metal cation activity.
    /// </summary>
    public sealed class MetalOxidationReaction : ElectrochemicalReaction
    {
        private readonly double _metalCationConcentration;

        /// <summary>
        /// Initialises a new <see cref="MetalOxidationReaction"/> with the given environmental conditions.
        /// </summary>
        /// <param name="pH">Solution pH (default 8.0).</param>
        /// <param name="temperatureCelsius">Electrolyte temperature in oC (default 25.0).</param>
        /// <param name="metalCationConcentration">Dissolved metal cation concentration [M2-] (mol/L, default 1.0e-6).</param>
        public MetalOxidationReaction(double pH = 8.0, double temperatureCelsius = 25.0, double metalCationConcentration = 1.0e-6)
            : base(ReactionType.MetalOxidation, e0Vshe: -0.44, z: 2, pH, temperatureCelsius)
        {
            _metalCationConcentration = metalCationConcentration;
        }

        /// <summary>
        /// Equilibrium potential vs. SHE (V) with Nernst correction for dissolved metal cation concentration.
        /// E_eq = E0 − (RT/zF)·ln(10)·pH + (RT/zF)·ln([M^z+])
        /// </summary>
        public override double EquilibriumPotentialVshe =>
            base.EquilibriumPotentialVshe + ThermalVoltageV * Math.Log(_metalCationConcentration);

        /// <summary>Minimum cathodic limiting current density for LM box bounds (A/cm2).</summary>
        public override double IlimMinAcm2 => 1.0e-14;

        /// <summary>Maximum cathodic limiting current density for LM box bounds (A/cm2).</summary>
        public override double IlimMaxAcm2 => 1.0e-6;
    }
}
