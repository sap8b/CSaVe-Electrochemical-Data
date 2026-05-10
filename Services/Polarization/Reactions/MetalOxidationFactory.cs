using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSaVe_Electrochemical_Data.Services.Polarization.Reactions
{
    public class MetalOxidationFactory : ElectrochemicalReactionFactory
    {
        /// <summary>
        /// Dissolved metal cation concentration [M²⁺] (mol/L) used to compute the Nernst
        /// concentration correction on <see cref="MetalOxidationReaction.EquilibriumPotentialVshe"/>.
        /// Default is 1.0e-6 mol/L, which is typical for a passivating or mildly corroding surface.
        /// </summary>
        public double MetalCationConcentration { get; set; } = 1.0e-6;

        public override bool CanCreateReaction(ReactionType reactionName)
        {
            return reactionName == ReactionType.MetalOxidation;
        }

        public override ElectrochemicalReaction CreateReaction(double pH, double temperatureCelsius)
        {
            return new MetalOxidationReaction(pH, temperatureCelsius, MetalCationConcentration);
        }
    }
}
