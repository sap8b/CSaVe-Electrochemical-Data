using CSaVe_Electrochemical_Data.Services.Polarization.Reactions;

namespace CSaVe_Electrochemical_Data
{
    /// <summary>
    /// Represents the hydrogen evolution half-reaction (2H⁺ + 2e⁻ → H₂).
    /// Standard reduction potential: E₀ = 0.000 V vs. SHE at pH 0.
    /// Electrons transferred: z = 2.
    /// </summary>
    public sealed class HerReaction : ElectrochemicalReaction
    {
        /// <summary>
        /// Initialises a new <see cref="HerReaction"/> with the given environmental conditions.
        /// </summary>
        /// <param name="pH">Solution pH (default 8.0).</param>
        /// <param name="temperatureCelsius">Electrolyte temperature in °C (default 25.0).</param>
        public HerReaction(double pH = 8.0, double temperatureCelsius = 25.0)
            : base(ReactionType.HydrogenEvolution, e0Vshe: 0.0, z: 2, pH, temperatureCelsius) { }
    }
}
