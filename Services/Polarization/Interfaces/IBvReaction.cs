namespace CSaVe_Electrochemical_Data;

/// <summary>
/// Defines the thermodynamic and kinetic data for a single electrochemical half-reaction
/// used in the Butler-Volmer polarization curve model.
/// Implement this interface to add a new reaction without modifying <see cref="BvCurveFitter"/>
/// or any other existing class (Open/Closed Principle).
/// </summary>
public interface IBvReaction
{
    /// <summary>Descriptive name of the reaction (e.g., "HER", "ORR", "Metal").</summary>
    string Name { get; }

    /// <summary>Standard reduction potential vs. SHE (V) at pH = 0.</summary>
    double E0Vshe { get; }

    /// <summary>Number of electrons transferred in the half-reaction.</summary>
    int Z { get; }

    /// <summary>Solution pH.</summary>
    double pH { get; }

    /// <summary>Electrolyte temperature (°C).</summary>
    double TemperatureCelsius { get; }

    /// <summary>Electrolyte temperature (K).</summary>
    double TemperatureKelvin { get; }

    /// <summary>
    /// Equilibrium potential vs. SHE (V) evaluated from the Nernst equation:
    /// E_eq = E0 − (R·T / z·F) · ln(10) · pH.
    /// </summary>
    double EquilibriumPotentialVshe { get; }

    /// <summary>Thermal voltage V_T = R·T / (z·F) (V).</summary>
    double ThermalVoltageV { get; }
}
