namespace CSaVe_Electrochemical_Data.Services.Polarization.Reactions
{
    public sealed class CrOxidationFactory : MetalOxidationFactoryBase
    {
        public override ElectrochemicalReaction CreateReaction(ElectrolyteConditions electrolyte)
        {
            return new CrOxidationReaction(electrolyte.PH, electrolyte.TemperatureCelsius, electrolyte.MetalIonConcentrationM);
        }
    }
}
