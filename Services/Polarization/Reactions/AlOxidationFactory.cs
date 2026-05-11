namespace CSaVe_Electrochemical_Data.Services.Polarization.Reactions
{
    public sealed class AlOxidationFactory : MetalOxidationFactoryBase
    {
        public override ElectrochemicalReaction CreateReaction(ElectrolyteConditions electrolyte)
        {
            return new AlOxidationReaction(electrolyte.PH, electrolyte.TemperatureCelsius, electrolyte.MetalIonConcentrationM);
        }
    }
}
