namespace CSaVe_Electrochemical_Data.Services.Polarization.Reactions
{
    public readonly struct ElectrolyteConditions
    {
        public ElectrolyteConditions(double pH, double temperatureCelsius, double metalIonConcentrationM)
        {
            PH = pH;
            TemperatureCelsius = temperatureCelsius;
            MetalIonConcentrationM = metalIonConcentrationM;
        }

        public double PH { get; }
        public double TemperatureCelsius { get; }
        public double MetalIonConcentrationM { get; }
    }

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
        public abstract bool CanCreateReaction(ReactionType reactionName);
        public abstract ElectrochemicalReaction CreateReaction(ElectrolyteConditions electrolyte);

        // ── Convenience static factory methods ────────────────────────────────────────────────────
        /// <summary>Creates a <see cref="MetalOxidationReaction"/> via <see cref="MetalOxidationFactory"/>.</summary>
        public static ElectrochemicalReaction CreateMetalOxidation(double pH = 8.0, double temperatureCelsius = 25.0)
            => new MetalOxidationFactory().CreateReaction(new ElectrolyteConditions(pH, temperatureCelsius, 1.0e-6));

        /// <summary>Creates an <see cref="OrrReaction"/> via <see cref="ORRFactory"/>.</summary>
        public static ElectrochemicalReaction CreateOrr(double pH = 8.0, double temperatureCelsius = 25.0)
            => new ORRFactory().CreateReaction(new ElectrolyteConditions(pH, temperatureCelsius, 1.0e-6));

        /// <summary>Creates a <see cref="HerReaction"/> via <see cref="HERFactory"/>.</summary>
        public static ElectrochemicalReaction CreateHer(double pH = 8.0, double temperatureCelsius = 25.0)
            => new HERFactory().CreateReaction(new ElectrolyteConditions(pH, temperatureCelsius, 1.0e-6));
    }
}
