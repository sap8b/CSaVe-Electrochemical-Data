using CSaVe_Electrochemical_Data.Services.Polarization.Reactions;

using System;

namespace CSaVe_Electrochemical_Data
{
    /// <summary>
    /// Calculates aqueous metal-cation diffusivities and diffusion-limited metal-deposition
    /// current densities for the Butler-Volmer initialisation path.
    ///
    /// Literature diffusivities at 25 oC (cm2/s) are from standard electrochemistry tables
    /// (Bard &amp; Faulkner, Electrochemical Methods; CRC Handbook):
    /// Fe2+  = 0.719e-5, Cr3+ = 0.594e-5, Ni2+ = 0.679e-5,
    /// Mo species (approx.) = 0.700e-5, Cu2+ = 0.714e-5, Al3+ = 0.541e-5.
    ///
    /// Temperature scaling uses a simplified Stokes-Einstein proportionality with viscosity
    /// correction omitted:
    /// D(T) = D25 * (T_K / 298.15).
    ///
    /// Metal-cation limiting current density is computed with:
    /// i_lim = z * F * D * c / δ,
    /// where z is the electron count, F is Faraday's constant (C/mol),
    /// D is diffusivity (cm2/s), c is dissolved metal-cation concentration (mol/cm3),
    /// and δ is diffusion-layer thickness (cm).
    /// </summary>
    public static class MetalCationDiffusivityCalculator
    {
        /// <summary>
        /// Calculates the aqueous metal-cation diffusivity (cm2/s) for the selected species
        /// and temperature.
        /// </summary>
        /// <param name="species">Metal cation species used for the BV metal reaction.</param>
        /// <param name="tempC">Electrolyte temperature (oC).</param>
        /// <returns>Estimated cation diffusivity in cm2/s.</returns>
        public static double CalcDiffusivityCm2PerS(MetalSpecies species, double tempC)
        {
            double d25 = species switch
            {
                MetalSpecies.Fe => 0.719e-5,
                MetalSpecies.Cr => 0.594e-5,
                MetalSpecies.Ni => 0.679e-5,
                MetalSpecies.Mo => 0.700e-5,
                MetalSpecies.Cu => 0.714e-5,
                MetalSpecies.Al => 0.541e-5,
                _ => throw new ArgumentOutOfRangeException(nameof(species), species, "Unsupported metal species.")
            };

            double tempK = tempC + 273.15;
            return d25 * (tempK / 298.15);
        }

        /// <summary>
        /// Calculates the diffusion-limited metal-deposition current density (A/cm2):
        /// i_lim = z * F * D * c / δ.
        /// </summary>
        /// <param name="species">Metal cation species used for diffusivity lookup.</param>
        /// <param name="z">Number of electrons transferred by the selected metal half-reaction.</param>
        /// <param name="tempC">Electrolyte temperature (oC).</param>
        /// <param name="metalCationConcentrationMolPerCm3">Dissolved metal-cation concentration (mol/cm3).</param>
        /// <param name="diffLayerThicknessCm">Diffusion-layer thickness δ (cm).</param>
        /// <returns>Diffusion-limited metal current density in A/cm2.</returns>
        public static double CalcMetalIlimAcm2(
            MetalSpecies species,
            int z,
            double tempC,
            double metalCationConcentrationMolPerCm3,
            double diffLayerThicknessCm)
        {
            if (diffLayerThicknessCm <= 0.0)
                throw new ArgumentOutOfRangeException(nameof(diffLayerThicknessCm), "Diffusion-layer thickness must be > 0.");
            if (metalCationConcentrationMolPerCm3 < 0.0)
                throw new ArgumentOutOfRangeException(nameof(metalCationConcentrationMolPerCm3), "Metal-cation concentration must be >= 0.");

            double diffusivity = CalcDiffusivityCm2PerS(species, tempC);
            return z * ElectrochemicalConstants.F * diffusivity * metalCationConcentrationMolPerCm3 / diffLayerThicknessCm;
        }
    }
}
