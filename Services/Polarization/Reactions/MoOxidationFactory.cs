namespace CSaVe_Electrochemical_Data.Services.Polarization.Reactions
{
    public sealed class MoOxidationFactory : MetalOxidationFactoryBase
    {
        public override ElectrochemicalReaction CreateReaction(ElectrolyteConditions electrolyte)
        {
            return new MoOxidationReaction(electrolyte.PH, electrolyte.TemperatureCelsius, electrolyte.MetalIonConcentrationM);
        }
    }
}
