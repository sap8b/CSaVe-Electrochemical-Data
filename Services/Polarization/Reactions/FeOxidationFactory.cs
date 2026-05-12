namespace CSaVe_Electrochemical_Data.Services.Polarization.Reactions
{
    public sealed class FeOxidationFactory : MetalOxidationFactoryBase
    {
        public override ElectrochemicalReaction CreateReaction(ElectrolyteConditions electrolyte)
        {
            return new FeOxidationReaction(electrolyte.PH, electrolyte.TemperatureCelsius, electrolyte.MetalIonConcentrationM);
        }
    }
}
