namespace CSaVe_Electrochemical_Data;

/// <summary>
/// Represents the metal oxidation (dissolution) half-reaction (e.g. Fe → Fe²⁺ + 2e⁻).
/// Uses an empirically adjusted standard potential of E₀ = −0.54 V vs. SHE (rather than the
/// textbook −0.44 V for Fe/Fe²⁺) to match the observed polarization curve, which may reflect
/// a reference electrode offset.
/// Electrons transferred: z = 2.
/// </summary>
public sealed class MetalOxidationReaction : ElectrochemicalReaction
{
    /// <summary>
    /// Initialises a new <see cref="MetalOxidationReaction"/> with the given environmental conditions.
    /// </summary>
    /// <param name="pH">Solution pH (default 8.0).</param>
    /// <param name="temperatureCelsius">Electrolyte temperature in °C (default 25.0).</param>
    public MetalOxidationReaction(double pH = 8.0, double temperatureCelsius = 25.0)
        : base("Metal", e0Vshe: -0.54, z: 2, pH, temperatureCelsius) { }
}
