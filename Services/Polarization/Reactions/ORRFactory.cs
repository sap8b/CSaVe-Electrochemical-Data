using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSaVe_Electrochemical_Data.Services.Polarization.Reactions
{
    public class ORRFactory : ElectrochemicalReactionFactory
    {
        public override bool CanCreateReaction(ReactionType reactionName)
        {
            return reactionName == ReactionType.OxygenReduction;
        }

        public override ElectrochemicalReaction CreateReaction(ElectrolyteConditions electrolyte)
        {
            return new OrrReaction(electrolyte.PH, electrolyte.TemperatureCelsius);
        }
    }
}
