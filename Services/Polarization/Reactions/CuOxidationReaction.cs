using CSaVe_Electrochemical_Data.Services.Polarization.Reactions;

namespace CSaVe_Electrochemical_Data
{
    /// <summary>Cu/Cu²⁺ oxidation reaction with E° = +0.34 V vs. SHE.</summary>
    public sealed class CuOxidationReaction : MetalOxidationReactionBase
    {
        public CuOxidationReaction(double pH = 8.0, double temperatureCelsius = 25.0, double metalCationConcentration = 1.0e-6)
            : base(e0Vshe: 0.34, z: 2, pH, temperatureCelsius, metalCationConcentration)
        {
        }
    }
}
