using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSaVe_Electrochemical_Data.Services.Polarization.Reactions
{
    public class MetalOxidationFactory : ElectrochemicalReactionFactory
    {
        public override bool CanCreateReaction(ReactionType reactionName)
        {
            return reactionName == ReactionType.MetalOxidation;
        }

        public override ElectrochemicalReaction CreateReaction(double pH, double temperatureCelsius)
        {
            return new MetalOxidationReaction(pH, temperatureCelsius);
        }
    }
}
