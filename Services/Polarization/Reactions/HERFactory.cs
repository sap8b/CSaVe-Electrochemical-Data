using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSaVe_Electrochemical_Data.Services.Polarization.Reactions
{
    public class HERFactory : ElectrochemicalReactionFactory
    {
        public override bool CanCreateReaction(ReactionType reactionName)
        {
            return reactionName == ReactionType.HydrogenEvolution;
        }

        public override ElectrochemicalReaction CreateReaction(double pH, double temperatureCelsius)
        {
            return new HerReaction(pH, temperatureCelsius);
        }
    }
}
