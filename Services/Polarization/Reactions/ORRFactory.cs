using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSaVe_Electrochemical_Data.Services.Polarization.Reactions
{
    public class ORRFactory : ElectrochemicalReactionFactory
    {
        public override bool canCreateReaction(ReactionType reactionName)
        {
            return reactionName == ReactionType.OxygenReduction;
        }

        public override ElectrochemicalReaction CreateReaction(double pH, double temperatureCelsius)
        {
            return new OrrReaction(pH, temperatureCelsius);
        }
    }
}
