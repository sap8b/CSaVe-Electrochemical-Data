using CSaVe_Electrochemical_Data.Services.Polarization.Reactions;

namespace CSaVe_Electrochemical_Data
{
    /// <summary>Al/Al³⁺ oxidation reaction with E° = -1.66 V vs. SHE.</summary>
    public sealed class AlOxidationReaction : MetalOxidationReactionBase
    {
        public AlOxidationReaction(double pH = 8.0, double temperatureCelsius = 25.0, double metalCationConcentration = 1.0e-6)
            : base(e0Vshe: -1.66, z: 3, pH, temperatureCelsius, metalCationConcentration)
        {
        }
    }
}
