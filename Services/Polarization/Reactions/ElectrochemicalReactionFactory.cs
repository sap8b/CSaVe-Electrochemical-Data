namespace CSaVe_Electrochemical_Data.Services.Polarization.Reactions
{
    /// <summary>
    /// Factory that creates the <see cref="IBvReaction"/> instances used in the Butler-Volmer
    /// polarization curve model.
    /// Centralising construction here means that <see cref="BvCurveFitter"/> and other consumers
    /// request reactions by name/intent rather than constructing them directly, satisfying the
    /// Dependency-Inversion Principle and making it straightforward to add new reactions in the
    /// future without modifying existing fitter code.
    /// </summary>
    public abstract class ElectrochemicalReactionFactory
    {
        public abstract bool canCreateReaction(ReactionType reactionName);
        public abstract ElectrochemicalReaction CreateReaction(double pH, double temperatureCelsius);
    }
}
