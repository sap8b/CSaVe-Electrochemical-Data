using CSaVe_Electrochemical_Data.Services.Polarization.Reactions;

namespace CSaVe_Electrochemical_Data
{
    /// <summary>Mo/Mo³⁺ oxidation reaction with E° = -0.20 V vs. SHE.</summary>
    public sealed class MoOxidationReaction : MetalOxidationReactionBase
    {
        public MoOxidationReaction(double pH = 8.0, double temperatureCelsius = 25.0, double metalCationConcentration = 1.0e-6)
            : base(e0Vshe: -0.20, z: 3, pH, temperatureCelsius, metalCationConcentration)
        {
        }
    }
}
