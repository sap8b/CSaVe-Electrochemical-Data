using CSaVe_Electrochemical_Data.Services.Polarization.Reactions;

using System;

namespace CSaVe_Electrochemical_Data
{
    /// <summary>
    /// Shared implementation for metal-oxidation half-reactions with a dissolved-metal Nernst term.
    /// </summary>
    public abstract class MetalOxidationReactionBase : ElectrochemicalReaction
    {
        private readonly double _metalCationConcentration;

        protected MetalOxidationReactionBase(
            double e0Vshe,
            int z,
            double pH = 8.0,
            double temperatureCelsius = 25.0,
            double metalCationConcentration = 1.0e-6)
            : base(ReactionType.MetalOxidation, e0Vshe, z, pH, temperatureCelsius)
        {
            _metalCationConcentration = metalCationConcentration;
        }

        public override double EquilibriumPotentialVshe =>
            // Natural log is correct here because the Nernst term is formulated directly with RT/zF.
            base.EquilibriumPotentialVshe + ThermalVoltageV * Math.Log(_metalCationConcentration);

        public override double IlimMinAcm2 => 1.0e-14;

        public override double IlimMaxAcm2 => 1.0e-6;
    }
}
