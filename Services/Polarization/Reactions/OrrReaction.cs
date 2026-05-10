using CSaVe_Electrochemical_Data.Services.Polarization.Reactions;

namespace CSaVe_Electrochemical_Data
{
    /// <summary>
    /// Represents the oxygen reduction half-reaction (O₂ + 2H₂O + 4e⁻ → 4OH⁻).
    /// Standard reduction potential: E₀ = 1.229 V vs. SHE at pH 0.
    /// Electrons transferred: z = 4.
    /// </summary>
    public sealed class OrrReaction : ElectrochemicalReaction
    {
        /// <summary>
        /// Initialises a new <see cref="OrrReaction"/> with the given environmental conditions.
        /// </summary>
        /// <param name="pH">Solution pH (default 8.0).</param>
        /// <param name="temperatureCelsius">Electrolyte temperature in °C (default 25.0).</param>
        public OrrReaction(double pH = 8.0, double temperatureCelsius = 25.0)
            : base(ReactionType.OxygenReduction, e0Vshe: 1.229, z: 4, pH, temperatureCelsius) { }

        /// <summary>Minimum ORR limiting current density for LM box bounds (A/cm²).</summary>
        public override double IlimMinAcm2 => 1.0e-10;

        /// <summary>Maximum ORR limiting current density for LM box bounds (A/cm²).</summary>
        public override double IlimMaxAcm2 => 1.0;
    }
}
