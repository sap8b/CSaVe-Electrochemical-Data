using CSaVe_Electrochemical_Data.Services.Polarization.Reactions;

namespace CSaVe_Electrochemical_Data
{
    /// <summary>Fe/Fe²⁺ oxidation reaction with E° = -0.44 V vs. SHE.</summary>
    public sealed class FeOxidationReaction : MetalOxidationReactionBase
    {
        public FeOxidationReaction(double pH = 8.0, double temperatureCelsius = 25.0, double metalCationConcentration = 1.0e-6)
            : base(e0Vshe: -0.44, z: 2, pH, temperatureCelsius, metalCationConcentration)
        {
        }
    }
}
