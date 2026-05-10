namespace CSaVe_Electrochemical_Data;

/// <summary>
/// Factory that creates the <see cref="IBvReaction"/> instances used in the Butler-Volmer
/// polarization curve model.
/// Centralising construction here means that <see cref="BvCurveFitter"/> and other consumers
/// request reactions by name/intent rather than constructing them directly, satisfying the
/// Dependency-Inversion Principle and making it straightforward to add new reactions in the
/// future without modifying existing fitter code.
/// </summary>
public static class BvReactionFactory
{
    /// <summary>
    /// Creates the hydrogen evolution reaction (HER) half-reaction.
    /// 2H⁺ + 2e⁻ → H₂; E₀ = 0.000 V vs. SHE.
    /// </summary>
    /// <param name="pH">Solution pH (default 8.0).</param>
    /// <param name="temperatureCelsius">Electrolyte temperature in °C (default 25.0).</param>
    public static IBvReaction CreateHer(double pH = 8.0, double temperatureCelsius = 25.0) =>
        new HerReaction(pH, temperatureCelsius);

    /// <summary>
    /// Creates the oxygen reduction reaction (ORR) half-reaction.
    /// O₂ + 2H₂O + 4e⁻ → 4OH⁻; E₀ = 1.229 V vs. SHE.
    /// </summary>
    /// <param name="pH">Solution pH (default 8.0).</param>
    /// <param name="temperatureCelsius">Electrolyte temperature in °C (default 25.0).</param>
    public static IBvReaction CreateOrr(double pH = 8.0, double temperatureCelsius = 25.0) =>
        new OrrReaction(pH, temperatureCelsius);

    /// <summary>
    /// Creates the metal oxidation (dissolution) half-reaction.
    /// E.g. Fe → Fe²⁺ + 2e⁻; E₀ = −0.54 V vs. SHE (empirically adjusted).
    /// </summary>
    /// <param name="pH">Solution pH (default 8.0).</param>
    /// <param name="temperatureCelsius">Electrolyte temperature in °C (default 25.0).</param>
    public static IBvReaction CreateMetalOxidation(double pH = 8.0, double temperatureCelsius = 25.0) =>
        new MetalOxidationReaction(pH, temperatureCelsius);
}
