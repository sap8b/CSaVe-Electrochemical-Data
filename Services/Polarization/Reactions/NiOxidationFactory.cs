namespace CSaVe_Electrochemical_Data.Services.Polarization.Reactions
{
    public sealed class NiOxidationFactory : MetalOxidationFactoryBase
    {
        public override ElectrochemicalReaction CreateReaction(ElectrolyteConditions electrolyte)
        {
            return new NiOxidationReaction(electrolyte.PH, electrolyte.TemperatureCelsius, electrolyte.MetalIonConcentrationM);
        }
    }
}
