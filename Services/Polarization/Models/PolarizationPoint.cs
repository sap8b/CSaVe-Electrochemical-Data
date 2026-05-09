namespace CSaVe_Electrochemical_Data;

/// <summary>
/// An immutable (potential, current) data point from a polarization experiment.
/// </summary>
public readonly struct PolarizationPoint
{
    /// <summary>Electrode potential in Volts vs. reference.</summary>
    public double PotentialV { get; init; }

    /// <summary>Measured current in Amperes (signed; positive = anodic).</summary>
    public double CurrentA { get; init; }

    /// <summary>
    /// Returns the signed current density in A/cm² for a given exposed area.
    /// </summary>
    /// <param name="exposedAreaCm2">Exposed electrode area in cm². Must be &gt; 0.</param>
    /// <returns>Signed current density in A/cm².</returns>
    public double CurrentDensityAcm2(double exposedAreaCm2) => CurrentA / exposedAreaCm2;
}
