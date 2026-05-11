using CSaVe_Electrochemical_Data.Services.Polarization.Reactions;

namespace CSaVe_Electrochemical_Data
{
    /// <summary>Cr/Cr³⁺ oxidation reaction with E° = -0.74 V vs. SHE.</summary>
    public sealed class CrOxidationReaction : MetalOxidationReactionBase
    {
        public CrOxidationReaction(double pH = 8.0, double temperatureCelsius = 25.0, double metalCationConcentration = 1.0e-6)
            : base(e0Vshe: -0.74, z: 3, pH, temperatureCelsius, metalCationConcentration)
        {
        }
    }
}
