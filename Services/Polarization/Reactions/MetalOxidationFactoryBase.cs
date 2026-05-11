namespace CSaVe_Electrochemical_Data.Services.Polarization.Reactions
{
    public abstract class MetalOxidationFactoryBase : ElectrochemicalReactionFactory
    {
        public override bool CanCreateReaction(ReactionType reactionName)
        {
            return reactionName == ReactionType.MetalOxidation;
        }
    }
}
