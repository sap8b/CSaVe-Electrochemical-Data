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

        // ── Convenience static factory methods ────────────────────────────────────────────────────
        /// <summary>Creates a <see cref="MetalOxidationReaction"/> via <see cref="MetalOxidationFactory"/>.</summary>
        public static ElectrochemicalReaction CreateMetalOxidation(double pH = 8.0, double temperatureCelsius = 25.0)
            => new MetalOxidationFactory().CreateReaction(pH, temperatureCelsius);

        /// <summary>Creates an <see cref="OrrReaction"/> via <see cref="ORRFactory"/>.</summary>
        public static ElectrochemicalReaction CreateOrr(double pH = 8.0, double temperatureCelsius = 25.0)
            => new ORRFactory().CreateReaction(pH, temperatureCelsius);

        /// <summary>Creates a <see cref="HerReaction"/> via <see cref="HERFactory"/>.</summary>
        public static ElectrochemicalReaction CreateHer(double pH = 8.0, double temperatureCelsius = 25.0)
            => new HERFactory().CreateReaction(pH, temperatureCelsius);
    }
}
