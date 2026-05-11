namespace CSaVe_Electrochemical_Data.Services.Polarization.Reactions
{
    public readonly struct ElectrolyteConditions
    {
        public ElectrolyteConditions(double pH, double temperatureCelsius, double metalIonConcentrationM, MetalSpecies metalSpecies)
        {
            PH = pH;
            TemperatureCelsius = temperatureCelsius;
            MetalIonConcentrationM = metalIonConcentrationM;
            MetalSpecies = metalSpecies;
        }

        public double PH { get; }
        public double TemperatureCelsius { get; }
        public double MetalIonConcentrationM { get; }
        public MetalSpecies MetalSpecies { get; }
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
        public static ElectrochemicalReactionFactory CreateMetalOxidationFactory(MetalSpecies metalSpecies)
            => metalSpecies switch
            {
                MetalSpecies.Fe => new FeOxidationFactory(),
                MetalSpecies.Cr => new CrOxidationFactory(),
                MetalSpecies.Ni => new NiOxidationFactory(),
                MetalSpecies.Mo => new MoOxidationFactory(),
                MetalSpecies.Cu => new CuOxidationFactory(),
                MetalSpecies.Al => new AlOxidationFactory(),
                _ => new FeOxidationFactory()
            };

        /// <summary>Creates a selected metal-oxidation reaction via the matching species factory.</summary>
        public static ElectrochemicalReaction CreateMetalOxidation(MetalSpecies metalSpecies = MetalSpecies.Fe, double pH = 8.0, double temperatureCelsius = 25.0, double metalIonConcentrationM = 1.0e-6)
            => CreateMetalOxidationFactory(metalSpecies).CreateReaction(new ElectrolyteConditions(pH, temperatureCelsius, metalIonConcentrationM, metalSpecies));

        /// <summary>Creates an <see cref="OrrReaction"/> via <see cref="ORRFactory"/>.</summary>
        public static ElectrochemicalReaction CreateOrr(double pH = 8.0, double temperatureCelsius = 25.0)
            => new ORRFactory().CreateReaction(new ElectrolyteConditions(pH, temperatureCelsius, 1.0e-6, MetalSpecies.Fe));

        /// <summary>Creates a <see cref="HerReaction"/> via <see cref="HERFactory"/>.</summary>
        public static ElectrochemicalReaction CreateHer(double pH = 8.0, double temperatureCelsius = 25.0)
            => new HERFactory().CreateReaction(new ElectrolyteConditions(pH, temperatureCelsius, 1.0e-6, MetalSpecies.Fe));
    }
}
